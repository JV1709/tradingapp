namespace Model.Domain
{
    public class Holding
    {
        public required string Symbol { get; set; }
        public long TotalQuantity { get; set; }
        public long AvailableQuantity { get; set; }
    }
}
