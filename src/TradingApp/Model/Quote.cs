namespace Model
{
    public class Quote
    {
        public required string Symbol { get; set; }
        public decimal BidPrice { get; set; }
        public decimal AskPrice { get; set; }
        public DateTime Timestamp { get; set; }
    }
}
