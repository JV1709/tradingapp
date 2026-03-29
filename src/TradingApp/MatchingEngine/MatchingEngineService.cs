using Microsoft.Extensions.Options;
using Model.Config;
using Model.Event;
using Model.Domain;
using Infrastructure.Event;

namespace MatchingEngine
{
    public class MatchingEngineService : BackgroundService, IEventHandler<OrderUpdateEvent>
    {
        private readonly ILogger<MatchingEngineService> _logger;
        private readonly MarketConfig _marketConfig;
        private readonly ISystemAggregatorConsumerFactory _aggregatorFactory;
        private readonly IOrderBookConsumerFactory _orderBookConsumerFactory;
        private readonly IOrderBookFactory _orderBookFactory;
        private readonly IEventBus _eventBus;

        private readonly List<SystemAggregatorConsumer> _aggregators = new();
        private readonly List<OrderBookConsumer> _orderBookConsumers = new();

        public MatchingEngineService(
            ILogger<MatchingEngineService> logger,
            IOptions<MarketConfig> marketConfig,
            ISystemAggregatorConsumerFactory aggregatorFactory,
            IOrderBookConsumerFactory orderBookConsumerFactory,
            IOrderBookFactory orderBookFactory,
            IEventBus eventBus)
        {
            _logger = logger;
            _marketConfig = marketConfig.Value;
            _aggregatorFactory = aggregatorFactory;
            _orderBookConsumerFactory = orderBookConsumerFactory;
            _orderBookFactory = orderBookFactory;
            _eventBus = eventBus;

            _eventBus.Subscribe(this);
        }

        public override async Task StartAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Starting MatchingEngineService...");
            
            foreach (var instrument in _marketConfig.Instruments)
            {
                var symbol = instrument.Symbol;
                
                var aggregator = _aggregatorFactory.Create(symbol);
                var orderBookConsumer = _orderBookConsumerFactory.Create(symbol);
                
                _aggregators.Add(aggregator);
                _orderBookConsumers.Add(orderBookConsumer);
                
                _ = aggregator.StartAsync(cancellationToken);
                _ = orderBookConsumer.StartAsync(cancellationToken);
            }

            await base.StartAsync(cancellationToken);
        }

        public override async Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Stopping MatchingEngineService...");

            _eventBus.Unsubscribe(this);

            var stopTasks = _aggregators.Select(x => x.StopAsync(cancellationToken))
                .Concat(_orderBookConsumers.Select(x => x.StopAsync(cancellationToken)));

            await Task.WhenAll(stopTasks);

            _aggregators.ForEach(x => x.Dispose());
            _orderBookConsumers.ForEach(x => x.Dispose());

            _aggregators.Clear();
            _orderBookConsumers.Clear();

            await base.StopAsync(cancellationToken);
        }

        protected override Task ExecuteAsync(CancellationToken stoppingToken)
        {
            return Task.CompletedTask;
        }

        public Task HandleAsync(OrderUpdateEvent @event, CancellationToken cancellationToken = default)
        {
            if (@event.Order.Status == OrderStatus.Filled)
            {
                var book = _orderBookFactory.GetOrderBook(@event.Order.Symbol);
                book.RemoveFromCancellationCache(@event.Order.OrderId);
            }
            return Task.CompletedTask;
        }
    }
}
