using Model.Domain;

namespace Model.Event
{
    public record NewQuoteEvent : EventBase
    {
        public required Quote Quote { get; set; }
    }
}
