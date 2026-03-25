namespace Model.Domain
{
    public class Holding
    {
        public required string Symbol { get; set; }
        public long Quantity { get; set; }
    }
}
