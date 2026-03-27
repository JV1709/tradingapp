namespace Model.Event
{
    public record OrderCancelRejectedEvent : EventBase
    {
        public required string OrderId { get; set; }
        public required string RejectionReason { get; set; }
    }
}
