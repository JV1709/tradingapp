using Model.Domain;
using System.Collections.Concurrent;

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

        public bool TryAdd(Quote quote)
        {
            ArgumentNullException.ThrowIfNull(quote);
            return _quotesBySymbol.TryAdd(quote.Symbol, quote);
        }

        public void AddOrUpdate(Quote quote)
        {
            ArgumentNullException.ThrowIfNull(quote);
            _quotesBySymbol.AddOrUpdate(quote.Symbol, quote, (_, _) => quote);
        }

        public bool TryGet(string symbol, out Quote quote)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(symbol);
            return _quotesBySymbol.TryGetValue(symbol, out quote!);
        }

        public IReadOnlyCollection<Quote> GetAll()
        {
            return _quotesBySymbol.Values.ToArray();
        }

        public bool Remove(string symbol)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(symbol);
            return _quotesBySymbol.TryRemove(symbol, out _);
        }
    }
}
