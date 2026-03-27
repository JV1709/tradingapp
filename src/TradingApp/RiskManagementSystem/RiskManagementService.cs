using Microsoft.Extensions.Options;
using Model.Config;

namespace RiskManagementSystem
{
    public sealed class RiskManagementService : BackgroundService
    {
        private readonly ILogger<RiskManagementService> _logger;
        private readonly IRiskManagementConsumerFactory _consumerFactory;
        private readonly ParallelismConfig _config;
        private readonly List<RiskManagementConsumer> _consumers = new();

        public RiskManagementService(ILogger<RiskManagementService> logger, IRiskManagementConsumerFactory consumerFactory, IOptions<ParallelismConfig> config)
        {
            _logger = logger;
            _consumerFactory = consumerFactory;
            _config = config.Value;
        }

        public override async Task StartAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Starting RiskManagementService...");

            foreach (var partitionId in Enumerable.Range(0, _config.PartitionCount))
            {
                var consumer = _consumerFactory.Create(partitionId);
                _consumers.Add(consumer);
                _ = consumer.StartAsync(cancellationToken);
            }

            await base.StartAsync(cancellationToken);
        }

        public override async Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Stopping RiskManagementService...");

            var stopTasks = _consumers.Select(consumer => consumer.StopAsync(cancellationToken));
            await Task.WhenAll(stopTasks);
            _consumers.Clear();

            await base.StopAsync(cancellationToken);
        }

        protected override Task ExecuteAsync(CancellationToken stoppingToken)
        {
            // Background tasks are handled by consumers
            return Task.CompletedTask;
        }
    }
}
