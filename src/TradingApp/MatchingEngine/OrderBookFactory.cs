using System.Collections.Concurrent;

namespace MatchingEngine
{
    public interface IOrderBookFactory
    {
        OrderBook GetOrderBook(string symbol);
    }

    public class OrderBookFactory : IOrderBookFactory
    {
        private readonly ConcurrentDictionary<string, OrderBook> _orderBooks = new();

        public OrderBook GetOrderBook(string symbol)
        {
            return _orderBooks.GetOrAdd(symbol, s => new OrderBook(s));
        }
    }
}
