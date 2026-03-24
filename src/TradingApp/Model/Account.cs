namespace Model
{
    public class Account
    {
        public required string Username { get; set; }
        public decimal TotalBalance { get; set; }
        public decimal AvailableBalance { get; set; }
        public List<Holding> Holdings { get; set; } = new();
    }
}
