namespace TradingClient
{
    public static class TradingClientExceptions
    {
        public class UnexpectedErrorException : Exception
        {
            public UnexpectedErrorException(string message) : base(message)
            {
            }
        }

        public class QuoteNotFoundException : Exception
        {
            public QuoteNotFoundException(string symbol) : base($"Quote for symbol '{symbol}' not found.")
            {
            }
        }

        public class AccountNotFoundException : Exception
        {
            public AccountNotFoundException(string username) : base($"Account with username '{username}' not found.")
            {
            }
        }

        public class BadRequestException : Exception
        {
            public BadRequestException(string message) : base(message)
            {
            }
        }

        public class InstrumentsNotFoundException : Exception
        {
            public InstrumentsNotFoundException() : base("No instruments found.")
            {
            }
        }
    }
}
