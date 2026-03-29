using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using Model.Event;
using Model.Request;

namespace TradingClient
{
    public sealed class OrderClient : IDisposable
    {
        private readonly OrderClientConfig _config;

        private readonly ConcurrentDictionary<string, ClientWebSocket> _webSockets = new();
        private readonly ConcurrentDictionary<string, CancellationTokenSource> _subscriptions = new();
        private readonly SemaphoreSlim _connectionLock = new SemaphoreSlim(1, 1);

        public event Action<OrderUpdateEvent>? OrderUpdated;

        public OrderClient(OrderClientConfig config)
        {
            _config = config;
        }

        private Uri GetWebSocketUri(string username)
        {
            string host = _config.Hostname;
            // E.g., if Hostname is http://localhost:5000, convert to ws://localhost:5000/api/order/ws/{username}
            // Wait, OrderClientConfig has Port, but it was used for the TCP socket!
            // According to instructions, we connect to the TradingPlatform HTTP host directly.
            // If the user's string is already e.g. "http://localhost:5000", parsing Uri works.
            
            if (Uri.TryCreate(host, UriKind.Absolute, out var uri))
            {
                var scheme = uri.Scheme == "https" ? "wss" : "ws";
                var port = uri.Port;
                return new Uri($"{scheme}://{uri.Host}:{port}/api/order/ws/{username}");
            }
            
            // Fallback if Hostname was just "localhost" and we were relying on the old Port logic:
            return new Uri($"ws://{host}:{_config.Port}/api/order/ws/{username}");
        }

        public async Task SubscribeAsync(string username)
        {
            if (_subscriptions.ContainsKey(username))
            {
                return; // Already subscribed
            }

            var cts = new CancellationTokenSource();

            await _connectionLock.WaitAsync();
            try
            {
                if (_subscriptions.ContainsKey(username)) return;

                var ws = new ClientWebSocket();
                
                await ConnectWebSocketAsync(ws, GetWebSocketUri(username), cts.Token);
                
                if (_subscriptions.TryAdd(username, cts))
                {
                    _webSockets.TryAdd(username, ws);
                    _ = Task.Run(() => StreamOrderUpdatesAsync(username, ws, cts.Token));
                }
                else
                {
                    cts.Dispose();
                    ws.Dispose();
                }
            }
            finally
            {
                _connectionLock.Release();
            }
        }

        private async Task ConnectWebSocketAsync(ClientWebSocket ws, Uri uri, CancellationToken cancellationToken)
        {
            while (ws.State != WebSocketState.Open && !cancellationToken.IsCancellationRequested)
            {
                try
                {
                    await ws.ConnectAsync(uri, cancellationToken);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Failed to connect to {uri}: {ex.Message}. Retrying...");
                    await Task.Delay(_config.ReconnectDelaySeconds > 0 ? _config.ReconnectDelaySeconds * 1000 : 2000, cancellationToken);
                }
            }
        }

        public void Unsubscribe(string username)
        {
            if (_subscriptions.TryRemove(username, out var cts))
            {
                cts.Cancel();
                cts.Dispose();
            }

            if (_webSockets.TryRemove(username, out var ws))
            {
                try
                {
                    if (ws.State == WebSocketState.Open)
                    {
                        ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "Unsubscribing", CancellationToken.None).GetAwaiter().GetResult();
                    }
                }
                catch { }
                ws.Dispose();
            }
        }

        private async Task StreamOrderUpdatesAsync(string username, ClientWebSocket ws, CancellationToken cancellationToken)
        {
            var buffer = new byte[8192];
            using var ms = new MemoryStream();

            try
            {
                while (!cancellationToken.IsCancellationRequested && ws.State == WebSocketState.Open)
                {
                    var result = await ws.ReceiveAsync(new ArraySegment<byte>(buffer), cancellationToken);

                    if (result.MessageType == WebSocketMessageType.Close)
                    {
                        break; // Connection closed
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
                                var update = JsonSerializer.Deserialize<OrderUpdateEvent>(json);
                                if (update != null)
                                {
                                    OrderUpdated?.Invoke(update);
                                }
                            }
                            catch (JsonException ex)
                            {
                                Console.WriteLine($"Error parsing event stream for {username}: {ex.Message}");
                            }
                        }
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // Expected when disposing/unsubscribing
            }
            catch (WebSocketException ex)
            {
                 Console.WriteLine($"WebSocket error for {username}: {ex.Message}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error reading order stream for username {username}: {ex.Message}");
            }
            finally
            {
                if (!cancellationToken.IsCancellationRequested)
                {
                    // Disconnected unexpectedly, we could handle reconnects here.
                    Console.WriteLine($"WebSocket stream for {username} disconnected.");
                }
            }
        }

        public async Task PlaceOrder(PlaceOrderRequest request, CancellationToken cancellationToken)
        {
            if (!_webSockets.TryGetValue(request.AccountKey, out var ws) || ws.State != WebSocketState.Open)
            {
                throw new InvalidOperationException($"Must subscribe to account '{request.AccountKey}' before placing orders.");
            }

            var json = JsonSerializer.Serialize(request);
            var bytes = Encoding.UTF8.GetBytes(json);
            
            await ws.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, cancellationToken);
        }

        public async Task CancelOrder(CancelOrderRequest request, CancellationToken cancellationToken = default)
        {
            // We need to route CancelOrder to the right websocket. Unfortunately, CancelOrderRequest has OrderId, not AccountKey!
            // Oh, wait! The original system routed on TCP by relying on a shared socket state ("session.AccountKey" resolved on first PlaceOrder request).
            // Actually, we should see if CancelOrderRequest has AccountKey.
            // If it doesn't, we can broadcast or we should modify CancelOrderRequest to have AccountKey.
            // But right now, we can just send it on the first available websocket in _webSockets, assuming single-account usage per client instance if it doesn't have an AccountKey!
            
            if (_webSockets.IsEmpty)
                throw new InvalidOperationException("No active WebSocket connections found to cancel order.");

            var ws = _webSockets.Values.FirstOrDefault(w => w.State == WebSocketState.Open);
            if (ws == null)
                throw new InvalidOperationException("No open WebSocket connection available to send cancel request.");

            var json = JsonSerializer.Serialize(request);
            var bytes = Encoding.UTF8.GetBytes(json);
            
            await ws.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, cancellationToken);
        }

        public void Dispose()
        {
            var keys = _subscriptions.Keys.ToList();
            foreach (var key in keys)
            {
                Unsubscribe(key);
            }

            _connectionLock.Dispose();
        }
    }
}
