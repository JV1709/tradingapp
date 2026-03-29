using Infrastructure.Event;
using Infrastructure.Queue;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Model.Domain;
using Model.Event;
using Model.Request;
using Repository;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Threading.Channels;

namespace OrderGateway
{
    public sealed class OrderGatewayService
    {
        private readonly ILogger<OrderGatewayService> _logger;
        private readonly IPartitionedMPSCQueueSystem<GatewayRequest> _requestOutQueue;
        private readonly IOrderRepository _orderRepository;
        private readonly IEventBus _eventBus;

        public OrderGatewayService(
            ILogger<OrderGatewayService> logger,
            [FromKeyedServices("AccountShardQueue1")] IPartitionedMPSCQueueSystem<GatewayRequest> requestOutQueue,
            IOrderRepository orderRepository,
            IEventBus eventBus)
        {
            _logger = logger;
            _requestOutQueue = requestOutQueue;
            _orderRepository = orderRepository;
            _eventBus = eventBus;
        }

        private class ChannelEventHandler : IEventHandler<OrderUpdateEvent>
        {
            private readonly ChannelWriter<OrderUpdateEvent> _writer;

            public ChannelEventHandler(ChannelWriter<OrderUpdateEvent> writer)
            {
                _writer = writer;
            }

            public async Task HandleAsync(OrderUpdateEvent @event, CancellationToken cancellationToken = default)
            {
                await _writer.WriteAsync(@event, cancellationToken);
            }
        }

        public async Task ProcessWebSocketSessionAsync(WebSocket webSocket, string accountKey, CancellationToken cancellationToken)
        {
            _logger.LogInformation("WebSocket session started for account {AccountKey}", accountKey);

            if (!_requestOutQueue.TryGetQueue(accountKey, out var queue) || queue == null)
            {
                _logger.LogWarning("Cannot accept WebSocket session for account {AccountKey} because queue is unknown or invalid", accountKey);
                await webSocket.CloseAsync(WebSocketCloseStatus.InternalServerError, "Invalid account", CancellationToken.None);
                return;
            }

            var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            var wsTask = Task.Run(() => ReadIncomingOrdersAsync(webSocket, accountKey, queue, cts.Token), cts.Token);
            var updatesTask = Task.Run(() => SendOrderUpdatesAsync(webSocket, accountKey, cts.Token), cts.Token);

            await Task.WhenAny(wsTask, updatesTask);
            cts.Cancel();

            try
            {
                await Task.WhenAll(wsTask, updatesTask);
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in WebSocket session for account {AccountKey}", accountKey);
            }

            _logger.LogInformation("WebSocket session ended for account {AccountKey}", accountKey);
        }

        private async Task ReadIncomingOrdersAsync(WebSocket webSocket, string accountKey, MPSCQueue<GatewayRequest> queue, CancellationToken cancellationToken)
        {
            var buffer = new byte[8192];
            using var ms = new MemoryStream();

            while (!cancellationToken.IsCancellationRequested && webSocket.State == WebSocketState.Open)
            {
                try
                {
                    var result = await webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), cancellationToken);

                    if (result.MessageType == WebSocketMessageType.Close)
                    {
                        await webSocket.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, "", CancellationToken.None);
                        break;
                    }

                    if (result.MessageType == WebSocketMessageType.Text)
                    {
                        ms.Write(buffer, 0, result.Count);

                        if (result.EndOfMessage)
                        {
                            var json = Encoding.UTF8.GetString(ms.ToArray());
                            ms.SetLength(0);

                            try
                            {
                                var document = JsonSerializer.Deserialize<JsonElement>(json);
                                GatewayRequest? request = null;

                                if (document.TryGetProperty("AccountKey", out _) && document.TryGetProperty("Symbol", out _))
                                {
                                    var placeOrder = document.Deserialize<PlaceOrderRequest>();
                                    if (placeOrder != null && placeOrder.AccountKey == accountKey)
                                    {
                                        request = GatewayRequest.FromPlaceOrder(placeOrder);
                                    }
                                }
                                else if (document.TryGetProperty("OrderId", out _))
                                {
                                    var cancelOrder = document.Deserialize<CancelOrderRequest>();
                                    if (cancelOrder != null)
                                    {
                                        request = GatewayRequest.FromCancelOrder(cancelOrder);
                                    }
                                }

                                if (request != null)
                                {
                                    while (!queue.TryEnqueue(request) && !cancellationToken.IsCancellationRequested)
                                    {
                                        await Task.Delay(1, cancellationToken);
                                    }
                                }
                                else
                                {
                                    _logger.LogWarning("Unrecognized or invalid message shape from WebSocket from account {AccountKey}", accountKey);
                                }
                            }
                            catch (JsonException ex)
                            {
                                _logger.LogWarning(ex, "Failed to deserialize JSON from WebSocket from account {AccountKey}", accountKey);
                            }
                        }
                    }
                }
                catch (WebSocketException)
                {
                    // Usually means client disconnected unexpectedly
                    break;
                }
            }
        }

        private async Task SendOrderUpdatesAsync(WebSocket webSocket, string accountKey, CancellationToken cancellationToken)
        {
            var channel = Channel.CreateUnbounded<OrderUpdateEvent>();
            var handler = new ChannelEventHandler(channel.Writer);

            _eventBus.Subscribe(handler);

            try
            {
                var initialOrders = _orderRepository.GetByAccountKey(accountKey);
                foreach (var order in initialOrders)
                {
                    var initialEvent = new OrderUpdateEvent { Order = order, Remark = "snapshot" };
                    var bytes = JsonSerializer.SerializeToUtf8Bytes(initialEvent);
                    
                    if (webSocket.State == WebSocketState.Open)
                    {
                        await webSocket.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, cancellationToken);
                    }
                }

                await foreach (var orderUpdate in channel.Reader.ReadAllAsync(cancellationToken))
                {
                    if (webSocket.State != WebSocketState.Open)
                        break;

                    if (orderUpdate.Order.AccountKey != accountKey)
                        continue;

                    var bytes = JsonSerializer.SerializeToUtf8Bytes(orderUpdate);
                    
                    // Simple lock or synchronization isn't strictly needed here because
                    // SendAsync is thread-affine but only being called by this Task loop.
                    await webSocket.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, cancellationToken);
                }
            }
            finally
            {
                _eventBus.Unsubscribe(handler);
            }
        }
    }
}
