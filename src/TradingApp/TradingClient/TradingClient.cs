namespace TradingClient
{
    public sealed class TradingClient : IDisposable
    {
        public readonly AccountClient AccountClient;
        public readonly PriceClient PriceClient;
        public readonly OrderClient OrderClient;

        public TradingClient(TradingClientConfig config)
        {
            AccountClient = new AccountClient(config.AccountClientConfig);
            PriceClient = new PriceClient(config.PriceClientConfig);
            OrderClient = new OrderClient(config.OrderClientConfig);
        }

        public void Dispose()
        {
            AccountClient.Dispose();
            PriceClient.Dispose();
            OrderClient.Dispose();
        }
    }
}
