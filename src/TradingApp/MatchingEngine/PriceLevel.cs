using Model.Domain;

namespace MatchingEngine
{
    public class PriceLevel
    {
        public decimal Price { get; }
        public Queue<Order> Orders { get; }
        
        public PriceLevel(decimal price)
        {
            Price = price;
            Orders = new Queue<Order>();
        }

        public void Enqueue(Order order)
        {
            Orders.Enqueue(order);
        }

        public Order Dequeue()
        {
            return Orders.Dequeue();
        }

        public Order Peek()
        {
            return Orders.Peek();
        }

        public int Count => Orders.Count;
    }
}
