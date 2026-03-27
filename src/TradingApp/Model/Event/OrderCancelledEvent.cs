namespace Model.Event
{
    public record OrderCancelledEvent : EventBase
    {
        public required string OrderId { get; init; }
    }
}
