namespace Model.Event
{
    public abstract record EventBase
    {
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
        public Guid EventId { get; set; } = Guid.NewGuid();
    }
}
