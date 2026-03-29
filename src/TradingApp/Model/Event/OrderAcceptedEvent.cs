namespace Model.Event
{
    public record OrderAcceptedEvent : EventBase
    {
        public required Guid OrderId { get; init; }
    }
}
