using Infrastructure;
using Model.Domain;

namespace RiskManagementSystem
{
    public class RiskManagementService : BackgroundService
    {
        private readonly ILogger<RiskManagementService> _logger;
        private readonly IProducerQueueSystem<GatewayRequest> _producerQueueSystem;

        public RiskManagementService(ILogger<RiskManagementService> logger, IProducerQueueSystem<GatewayRequest> producerQueueSystem)
        {
            _logger = logger;
            _producerQueueSystem = producerQueueSystem;
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
