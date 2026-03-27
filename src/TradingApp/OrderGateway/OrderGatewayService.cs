using Infrastructure.Queue;
using Microsoft.Extensions.Options;
using Model.Config;
using Model.Request;
using Serializer;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Text.Json;

namespace OrderGateway
{
    public sealed class OrderGatewayService : BackgroundService
    {
        private readonly ILogger<OrderGatewayService> _logger;
        private readonly OrderGatewayConfig _options;
        private readonly IMessageSerializer _serializer;
        private readonly IPartitionedMPSCQueueSystem<GatewayRequest> _requestOutQueue;
        private readonly ConcurrentDictionary<string, ConnectionSession> _sessionsByConnectionId = new();

        public OrderGatewayService(
            ILogger<OrderGatewayService> logger,
            IOptions<OrderGatewayConfig> options,
            [FromKeyedServices("AccountShardQueue1")] IPartitionedMPSCQueueSystem<GatewayRequest> requestOutQueue)
        {
            _logger = logger;
            _options = options.Value;
            _serializer = new MessageSerializer(_options.SerializerLengthPrefixBytes);
            _requestOutQueue = requestOutQueue;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            var listener = new TcpListener(IPAddress.Any, _options.Port);
            listener.Start();

            _logger.LogInformation("OrderGateway listening for TCP clients on port {Port}", _options.Port);

            try
            {
                while (!stoppingToken.IsCancellationRequested)
                {
                    var tcpClient = await listener.AcceptTcpClientAsync(stoppingToken);
                    var connectionId = Guid.NewGuid().ToString("N");

                    var session = new ConnectionSession(
                        connectionId,
                        tcpClient);

                    _sessionsByConnectionId[connectionId] = session;

                    _ = Task.Run(() => ProcessConnectionAsync(session, stoppingToken), stoppingToken);
                }
            }
            catch (OperationCanceledException)
            {
                // graceful shutdown
            }
            finally
            {
                listener.Stop();
            }
        }

        private async Task ProcessConnectionAsync(ConnectionSession session, CancellationToken cancellationToken)
        {
            var remote = session.Client.Client.RemoteEndPoint?.ToString() ?? "unknown";
            _logger.LogInformation("Accepted order connection {ConnectionId} from {Remote}", session.ConnectionId, remote);

            try
            {
                using var stream = session.Client.GetStream();

                while (!cancellationToken.IsCancellationRequested)
                {
                    var request = await TryReadGatewayRequestAsync(stream, cancellationToken);
                    if (request == null)
                    {
                        break;
                    }

                    var accountKey = request.PlaceOrderRequest?.AccountKey;

                    if (!string.IsNullOrWhiteSpace(accountKey) && string.IsNullOrWhiteSpace(session.AccountKey))
                    {
                        session.AccountKey = accountKey;
                    }

                    if (session.Queue == null && !string.IsNullOrWhiteSpace(session.AccountKey))
                    {
                        if (_requestOutQueue.TryGetQueue(session.AccountKey, out var queue) && queue != null)
                        {
                            session.Queue = queue;
                        }
                    }

                    if (session.Queue == null)
                    {
                        _logger.LogWarning("Cannot enqueue request for connection {ConnectionId} because account key is unknown or invalid", session.ConnectionId);
                        break;
                    }

                    while (!session.Queue.TryEnqueue(request) && !cancellationToken.IsCancellationRequested)
                        await Task.Delay(1, cancellationToken);
                }
            }
            catch (OperationCanceledException)
            {
                // graceful shutdown
            }
            catch (IOException ex)
            {
                _logger.LogInformation(ex, "Connection {ConnectionId} closed", session.ConnectionId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Connection {ConnectionId} failed", session.ConnectionId);
            }
            finally
            {
                _sessionsByConnectionId.TryRemove(session.ConnectionId, out _);
                session.Client.Dispose();
                _logger.LogInformation("Disconnected connection {ConnectionId} account {AccountKey}", session.ConnectionId, session.AccountKey ?? "unknown");
            }
        }

        private async Task<GatewayRequest?> TryReadGatewayRequestAsync(NetworkStream stream, CancellationToken cancellationToken)
        {
            try
            {
                var payload = await _serializer.DeserializeAsync<JsonElement>(stream, cancellationToken);
                if (payload.ValueKind != JsonValueKind.Object)
                {
                    _logger.LogWarning("Unrecognized message payload type from connection stream");
                    return null;
                }

                if (payload.TryGetProperty("accountKey", out _) && payload.TryGetProperty("symbol", out _))
                {
                    var placeOrder = payload.Deserialize<PlaceOrderRequest>();
                    if (placeOrder == null)
                    {
                        _logger.LogWarning("Failed to parse place-order payload from connection stream");
                        return null;
                    }

                    return GatewayRequest.FromPlaceOrder(placeOrder);
                }

                if (payload.TryGetProperty("orderId", out _))
                {
                    var cancelOrder = payload.Deserialize<CancelOrderRequest>();
                    if (cancelOrder == null)
                    {
                        _logger.LogWarning("Failed to parse cancel-order payload from connection stream");
                        return null;
                    }

                    return GatewayRequest.FromCancelOrder(cancelOrder);
                }

                _logger.LogWarning("Unrecognized message shape from connection stream");
                return null;
            }
            catch (EndOfStreamException)
            {
                return null;
            }
            catch (OperationCanceledException)
            {
                return null;
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogWarning(ex, "Failed to deserialize order gateway message from stream");
                return null;
            }
        }

        private sealed class ConnectionSession
        {
            public ConnectionSession(string connectionId, TcpClient client)
            {
                ConnectionId = connectionId;
                Client = client;
            }

            public string ConnectionId { get; }
            public TcpClient Client { get; }
            public MPSCQueue<GatewayRequest>? Queue { get; set; }
            public string? AccountKey { get; set; }
        }
    }
}
