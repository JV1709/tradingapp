using Model.Domain;

namespace Model.Event
{
    public record OrderUpdateEvent : EventBase
    {
        public required Order Order { get; set; }
        public required string Remark { get; set; }
    }
}
