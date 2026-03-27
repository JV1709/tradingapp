using Model.Domain;

namespace Model.Event
{
    public record AccountUpdateEvent : EventBase
    {
        public required string Username { get; set; }
        public decimal TotalBalance { get; set; }
        public decimal AvailableBalance { get; set; }
        public List<Holding> Holdings { get; set; } = new();
    }
}
