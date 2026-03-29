namespace Model.Event
{
    public record OrderMatchEvent : EventBase
    {
        public required Guid BuyOrderId { get; set; }
        public required Guid SellOrderId { get; set; }
        public long FillQuantity { get; set; }
        public decimal FillPrice { get; set; }
        public decimal? BidPrice { get; set; }
        public decimal? AskPrice { get; set; }
    }
}
