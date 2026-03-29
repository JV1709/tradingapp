using System;

namespace Model.Event
{
    public record OrderMatchEvent : EventBase
    {
        public required Guid OrderId { get; set; }
        public long FilledQuantity { get; set; }
        public decimal Price { get; set; }
    }
}
