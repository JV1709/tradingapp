using Model;
using System.Text.Json;
using System.Collections.Concurrent;

namespace TradingClient
{
    public sealed class AccountClient : IDisposable
    {
        private const string BaseUri = "account/stream/";
        private readonly HttpClient _httpClient;
        private readonly CancellationTokenSource _cts = new();
        private readonly ConcurrentDictionary<string, CancellationTokenSource> _subscriptions = new();

        // Event Bus
        public event Action<Account>? AccountUpdated;

        public AccountClient(AccountClientConfig config)
        {
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
                _ = Task.Run(() => StreamAccountAsync(username, linkedCts.Token));
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

        private async Task StreamAccountAsync(string username, CancellationToken cancellationToken)
        {
            try
            {
                using var stream = await _httpClient.GetStreamAsync(BaseUri + username, cancellationToken);
                using var reader = new StreamReader(stream);

                while (!cancellationToken.IsCancellationRequested && !reader.EndOfStream)
                {
                    var line = await reader.ReadLineAsync(cancellationToken);
                    if (!string.IsNullOrWhiteSpace(line))
                    {
                        var account = JsonSerializer.Deserialize<Account>(line);
                        if (account != null)
                        {
                            // Publish to event bus
                            AccountUpdated?.Invoke(account);
                        }
                    }
                }
            }
            catch (HttpRequestException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                throw new TradingClientExceptions.AccountNotFoundException(username);
            }
            catch (OperationCanceledException)
            {
                // Expected when disposing/unsubscribing
            }
            catch (Exception ex)
            {
                // Consider adding an Error event or logger here
                throw new TradingClientExceptions.UnexpectedErrorException($"Error reading account stream for username {username}: {ex.Message}");
            }
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
        }
    }
}
