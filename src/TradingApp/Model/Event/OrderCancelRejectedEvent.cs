using System;

namespace Model.Event
{
    public record OrderCancelRejectedEvent : EventBase
    {
        public required Guid OrderId { get; set; }
        public required string RejectionReason { get; set; }
    }
}
