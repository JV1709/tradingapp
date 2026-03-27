using Microsoft.Extensions.Options;
using Model.Config;

namespace OrderManagementSystem
{
    public class OrderManagementService : BackgroundService
    {
        private readonly ILogger<OrderManagementService> _logger;
        private readonly MarketConfig _marketConfig;
        private readonly IOrderQueueConsumerFactory _consumerFactory;
        private readonly List<OrderQueueConsumer> _consumers = new();

        public OrderManagementService(
            ILogger<OrderManagementService> logger,
            IOptions<MarketConfig> marketConfig,
            IOrderQueueConsumerFactory consumerFactory)
        {
            _logger = logger;
            _marketConfig = marketConfig.Value;
            _consumerFactory = consumerFactory;
        }

        public override async Task StartAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Starting OrderManagementService...");
            
            foreach (var instrument in _marketConfig.Instruments)
            {
                var symbol = instrument.Symbol;
                var consumer = _consumerFactory.Create(instrument.Symbol);
                _consumers.Add(consumer);
                
                _ = consumer.StartAsync(cancellationToken);
            }

            await base.StartAsync(cancellationToken);
        }

        public override async Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Stopping OrderManagementService...");

            var stopTasks = _consumers.Select(c => c.StopAsync(cancellationToken));
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
