using Model.Domain;
using System.Collections.Concurrent;
using System.Text.Json;

namespace Repository
{
    public interface IOrderRepository
    {
        bool TryAdd(Order order);
        void AddOrUpdate(Order order);
        bool TryGet(Guid orderId, out Order order);
        IReadOnlyCollection<Order> GetAll();
        IReadOnlyCollection<Order> GetByAccountKey(string accountKey);
        bool Remove(Guid orderId);
    }

    public class OrderRepository : IOrderRepository
    {
        private readonly ConcurrentDictionary<Guid, Order> _orders = new();

        private static Order Clone(Order order) => JsonSerializer.Deserialize<Order>(JsonSerializer.Serialize(order))!;

        public bool TryAdd(Order order)
        {
            ArgumentNullException.ThrowIfNull(order);
            return _orders.TryAdd(order.OrderId, Clone(order));
        }

        public void AddOrUpdate(Order order)
        {
            ArgumentNullException.ThrowIfNull(order);
            _orders.AddOrUpdate(order.OrderId, Clone(order), (_, _) => Clone(order));
        }

        public bool TryGet(Guid orderId, out Order order)
        {
            if (_orders.TryGetValue(orderId, out var storedOrder))
            {
                order = Clone(storedOrder);
                return true;
            }
            order = null!;
            return false;
        }

        public IReadOnlyCollection<Order> GetAll()
        {
            return _orders.Values.Select(Clone).ToArray();
        }

        public IReadOnlyCollection<Order> GetByAccountKey(string accountKey)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(accountKey);
            return _orders.Values
                .Where(o => string.Equals(o.AccountKey, accountKey, StringComparison.OrdinalIgnoreCase))
                .Select(Clone)
                .ToArray();
        }

        public bool Remove(Guid orderId)
        {
            return _orders.TryRemove(orderId, out _);
        }
    }
}
