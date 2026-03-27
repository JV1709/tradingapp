namespace Model.Event
{
    public record OrderAcceptedEvent : EventBase
    {
        public required string OrderId { get; init; }
    }
}
