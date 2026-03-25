using System.Text.Json;
using System.Collections.Concurrent;
using Model.Domain;

namespace TradingClient
{
    public sealed class PriceClient : IDisposable
    {
        private const string BaseUri = "quote/stream/";
        private readonly HttpClient _httpClient;
        private readonly CancellationTokenSource _cts = new();
        private readonly ConcurrentDictionary<string, CancellationTokenSource> _subscriptions = new();

        // Event Bus
        public event Action<Quote>? QuoteReceived;

        public PriceClient(PriceClientConfig config)
        {
            _httpClient = new HttpClient();
            _httpClient.BaseAddress = new Uri(config.Hostname);
        }

        public void Subscribe(string symbol, Action<Quote> eventHandler)
        {
            if (_subscriptions.ContainsKey(symbol))
            {
                return; // Already subscribed
            }

            var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(_cts.Token);
            if (_subscriptions.TryAdd(symbol, linkedCts))
            {
                // Start a long-running streaming subscription for the specific symbol
                _ = Task.Run(() => StreamQuotesAsync(symbol, linkedCts.Token));
            }
            else
            {
                linkedCts.Dispose();
            }
        }

        public void Unsubscribe(string symbol)
        {
            if (_subscriptions.TryRemove(symbol, out var linkedCts))
            {
                linkedCts.Cancel();
                linkedCts.Dispose();
            }
        }

        private async Task StreamQuotesAsync(string symbol, CancellationToken cancellationToken)
        {
            try
            {
                using var stream = await _httpClient.GetStreamAsync(BaseUri + symbol, cancellationToken);
                using var reader = new StreamReader(stream);

                while (!cancellationToken.IsCancellationRequested && !reader.EndOfStream)
                {
                    var line = await reader.ReadLineAsync(cancellationToken);
                    if (!string.IsNullOrWhiteSpace(line))
                    {
                        var quote = JsonSerializer.Deserialize<Quote>(line);
                        if (quote != null)
                        {
                            // Publish to event bus
                            QuoteReceived?.Invoke(quote);
                        }
                    }
                }
            }
            catch (HttpRequestException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                throw new TradingClientExceptions.QuoteNotFoundException(symbol);
            }
            catch (OperationCanceledException)
            {
                // Expected when disposing/unsubscribing
            }
            catch (Exception ex)
            {
                // Consider adding an Error event or logger here
                throw new TradingClientExceptions.UnexpectedErrorException($"Error reading quote stream for symbol {symbol}: {ex.Message}");
            }
        }

        public void Dispose()
        {
            _cts.Cancel();
            
            foreach (var (key, val) in _subscriptions)
            {
                val.Dispose();
            }
            _subscriptions.Clear();

            _cts.Dispose();
            _httpClient.Dispose();
        }
    }
}
