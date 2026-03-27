namespace Model.Event
{
    public record OrderRejectedEvent : EventBase
    {
        public Guid OrderId { get; init; }
        public required string RejectionReason { get; init; }
    }
}
