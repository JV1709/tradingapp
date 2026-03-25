using Model.Domain;
using System.Collections.Concurrent;

namespace Repository
{
    public interface IOrderRepository
    {
        bool TryAdd(Order order);
        void AddOrUpdate(Order order);
        bool TryGet(string orderId, out Order order);
        IReadOnlyCollection<Order> GetAll();
        bool Remove(string orderId);
    }

    public class OrderRepository : IOrderRepository
    {
        private readonly ConcurrentDictionary<string, Order> _orders = new(StringComparer.OrdinalIgnoreCase);

        public bool TryAdd(Order order)
        {
            ArgumentNullException.ThrowIfNull(order);
            return _orders.TryAdd(order.OrderId, order);
        }

        public void AddOrUpdate(Order order)
        {
            ArgumentNullException.ThrowIfNull(order);
            _orders.AddOrUpdate(order.OrderId, order, (_, _) => order);
        }

        public bool TryGet(string orderId, out Order order)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(orderId);
            return _orders.TryGetValue(orderId, out order!);
        }

        public IReadOnlyCollection<Order> GetAll()
        {
            return _orders.Values.ToArray();
        }

        public bool Remove(string orderId)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(orderId);
            return _orders.TryRemove(orderId, out _);
        }
    }
}
