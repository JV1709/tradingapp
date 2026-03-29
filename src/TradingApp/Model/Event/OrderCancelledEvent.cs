using System;

namespace Model.Event
{
    public record OrderCancelledEvent : EventBase
    {
        public required Guid OrderId { get; init; }
    }
}
