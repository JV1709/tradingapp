namespace Model.Domain
{
    public class Order
    {
        public required string OrderId { get; set; }
        public required string AccountKey { get; set; }
        public OrderStatus Status { get; set; }
        public required string Symbol { get; set; }
        public long TotalQuantity { get; set; }
        public long FilledQuantity { get; set; }
        public decimal Price { get; set; }
        // Side: 1 for buy, 2 for sell
        public Side Side { get; set; }
    }

    public class OrderUpdate
    {
        public required string OrderId { get; set; }
        public OrderStatus Status { get; set; }
        public long FilledQuantity { get; set; }
        public decimal FilledPrice { get; set; }
    }

    public enum OrderStatus
    {
        New = 0,
        PartiallyFilled = 1,
        Filled = 2,
        Cancelled = 4,
        PendingCancel = 6,
        Rejected = 8,
        PendingNew = 'A'
    }

    public enum Side
    {
        Buy = 1,
        Sell = 2
    }
}
