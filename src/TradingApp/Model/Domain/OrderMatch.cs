namespace Model.Domain
{
    public record OrderMatch
    {
        public Guid TakerOrderId { get; init; }
        public Guid MakerOrderId { get; init; }
        public decimal Price { get; init; }
        public int Quantity { get; init; }
        public decimal? BidPrice { get; init; }
        public decimal? AskPrice { get; init; }
    }
}
