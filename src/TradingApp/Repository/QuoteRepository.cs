using Model.Domain;
using System.Collections.Concurrent;
using System.Linq;
using System.Text.Json;

namespace Repository
{
    public interface IQuoteRepository
    {
        bool TryAdd(Quote quote);
        void AddOrUpdate(Quote quote);
        bool TryGet(string symbol, out Quote quote);
        IReadOnlyCollection<Quote> GetAll();
        bool Remove(string symbol);
    }

    public class QuoteRepository : IQuoteRepository
    {
        private readonly ConcurrentDictionary<string, Quote> _quotesBySymbol = new(StringComparer.OrdinalIgnoreCase);

        private static Quote Clone(Quote quote) => JsonSerializer.Deserialize<Quote>(JsonSerializer.Serialize(quote))!;

        public bool TryAdd(Quote quote)
        {
            ArgumentNullException.ThrowIfNull(quote);
            return _quotesBySymbol.TryAdd(quote.Symbol, Clone(quote));
        }

        public void AddOrUpdate(Quote quote)
        {
            ArgumentNullException.ThrowIfNull(quote);
            _quotesBySymbol.AddOrUpdate(quote.Symbol, Clone(quote), (_, _) => Clone(quote));
        }

        public bool TryGet(string symbol, out Quote quote)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(symbol);
            if (_quotesBySymbol.TryGetValue(symbol, out var storedQuote))
            {
                quote = Clone(storedQuote);
                return true;
            }
            quote = null!;
            return false;
        }

        public IReadOnlyCollection<Quote> GetAll()
        {
            return _quotesBySymbol.Values.Select(Clone).ToArray();
        }

        public bool Remove(string symbol)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(symbol);
            return _quotesBySymbol.TryRemove(symbol, out _);
        }
    }
}
