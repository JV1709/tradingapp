namespace OrderGateway
{
    public class GatewayWorker : BackgroundService
    {
        private readonly ILogger<GatewayWorker> _logger;

        public GatewayWorker(ILogger<GatewayWorker> logger)
        {
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                if (_logger.IsEnabled(LogLevel.Information))
                {
                    _logger.LogInformation("Worker running at: {time}", DateTimeOffset.Now);
                }
                await Task.Delay(1000, stoppingToken);
            }
        }
    }
}
