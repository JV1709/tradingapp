namespace TradingClient
{
    public class TradingClientConfig
    {
        public required AccountClientConfig AccountClientConfig { get; set; }
        public required PriceClientConfig PriceClientConfig { get; set; }
        public required OrderClientConfig OrderClientConfig { get; set; }
    }

    public class AccountClientConfig
    {
        // Domain name of the trading server, e.g., "http://localhost:5000" or "http://tradingserver.com"
        // Can also be an IP address
        public required string Hostname { get; set; }
    }

    public class PriceClientConfig
    {

        // Domain name of the trading server, e.g., "http://localhost:5000" or "http://tradingserver.com"
        // Can also be an IP address
        public required string Hostname { get; set; }
    }

    public class OrderClientConfig
    {

        // Domain name of the trading server, e.g., "http://localhost:5000" or "http://tradingserver.com"
        // Can also be an IP address
        public required string Hostname { get; set; }
        public int Port { get; set; }
        public int ReconnectDelaySeconds { get; set; }
        // Should be either 2, 4, or 8
        public int SerializerLengthPrefixBytes { get; set; }
    }
}
