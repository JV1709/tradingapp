using Model;
using Serializer;
using System.Net.Sockets;
using System.Text.Json;
using System.Collections.Concurrent;

namespace TradingClient
{
    public sealed class OrderClient : IDisposable
    {
        private const string StreamBaseUri = "order/stream/";
        private readonly OrderClientConfig _config;
        private readonly IMessageSerializer _serializer;

        private readonly TcpClient _tcpClient;
        private SemaphoreSlim _connectionLock = new SemaphoreSlim(1, 1);

        private readonly HttpClient _httpClient;
        private readonly CancellationTokenSource _cts = new();
        private readonly ConcurrentDictionary<string, CancellationTokenSource> _subscriptions = new();

        public event Action<OrderUpdate>? OrderUpdated;

        public OrderClient(OrderClientConfig config)
        {
            _config = config;
            _serializer = new MessageSerializer(config.SerializerLengthPrefixBytes);

            _tcpClient = new TcpClient();

            _httpClient = new HttpClient();
            _httpClient.BaseAddress = new Uri(config.Hostname);
        }

        public void Subscribe(string username)
        {
            if (_subscriptions.ContainsKey(username))
            {
                return; // Already subscribed
            }

            var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(_cts.Token);
            if (_subscriptions.TryAdd(username, linkedCts))
            {
                // Start a long-running streaming subscription for the specific account
                _ = Task.Run(() => StreamOrderUpdatesAsync(username, linkedCts.Token));
            }
            else
            {
                linkedCts.Dispose();
            }
        }

        public void Unsubscribe(string username)
        {
            if (_subscriptions.TryRemove(username, out var linkedCts))
            {
                linkedCts.Cancel();
                linkedCts.Dispose();
            }
        }

        private async Task StreamOrderUpdatesAsync(string username, CancellationToken cancellationToken)
        {
            try
            {
                using var stream = await _httpClient.GetStreamAsync(StreamBaseUri + username, cancellationToken);
                using var reader = new StreamReader(stream);

                while (!cancellationToken.IsCancellationRequested && !reader.EndOfStream)
                {
                    var line = await reader.ReadLineAsync(cancellationToken);
                    if (!string.IsNullOrWhiteSpace(line))
                    {
                        var update = JsonSerializer.Deserialize<OrderUpdate>(line);
                        if (update != null)
                        {
                            // Publish to event bus
                            OrderUpdated?.Invoke(update);
                        }
                    }
                }
            }
            catch (HttpRequestException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                throw new TradingClientExceptions.AccountNotFoundException($"Account (username '{username}') for order updates not found.");
            }
            catch (OperationCanceledException)
            {
                // Expected when disposing/unsubscribing
            }
            catch (Exception ex)
            {
                // Consider adding an Error event or logger here
                Console.WriteLine($"Error reading order stream for username {username}: {ex.Message}");
            }
        }

        public async Task PlaceOrder(PlaceOrderRequest request, CancellationToken cancellationToken)
        {
            if (!_tcpClient.Connected)
                await StartClientAsync(cancellationToken);

            var message = _serializer.Serialize(request);
            await _tcpClient.Client.SendAsync(message, cancellationToken);
        }

        public async Task CancelOrder(CancelOrderRequest request, CancellationToken cancellationToken = default)
        {
            if (!_tcpClient.Connected)
                await StartClientAsync(cancellationToken);

            var message = _serializer.Serialize(request);
            await _tcpClient.Client.SendAsync(message, cancellationToken);
        }

        public async Task StartClientAsync(CancellationToken cancellationToken = default)
        {
            if (_tcpClient.Connected)
                return;

            _connectionLock.Wait();
            while (!_tcpClient.Connected)
            {
                try
                {
                    await _tcpClient.ConnectAsync(_config.Hostname, _config.Port, cancellationToken);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Failed to connect: {ex.Message}. Retrying in {_config.ReconnectDelaySeconds} seconds...");
                    await Task.Delay(_config.ReconnectDelaySeconds * 1000);
                }
            }
            _connectionLock.Release();
        }

        public void Dispose()
        {
            _cts.Cancel();

            foreach (var kvp in _subscriptions)
            {
                kvp.Value.Dispose();
            }
            _subscriptions.Clear();

            _cts.Dispose();
            _httpClient.Dispose();

            _tcpClient.Dispose();
            _connectionLock.Dispose();
        }
    }
}
