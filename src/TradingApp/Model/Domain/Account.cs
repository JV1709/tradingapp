namespace Model.Domain
{
    public class Account
    {
        public required string Username { get; set; }
        public decimal TotalBalance { get; set; }
        public decimal AvailableBalance { get; set; }
        public List<Holding> Holdings { get; set; } = new();
    }

    public class CreateAccountRequest
    {
        public required string Username { get; set; }
        public decimal InitialBalance { get; set; }
    }
}
