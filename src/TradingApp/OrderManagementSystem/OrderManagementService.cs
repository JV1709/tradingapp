using Infrastructure.Event;
using Model.Config;
using Microsoft.Extensions.Options;
using Repository;

namespace OrderManagementSystem
{
    public class OrderManagementService : BackgroundService
    {
        private readonly ILogger<OrderManagementService> _logger;
        private readonly ParallelismConfig _parallelismConfig;
        private readonly IOrderQueueConsumerFactory _consumerFactory;
        private readonly List<OrderQueueConsumer> _consumers = new();
        
        private readonly IEventBus _eventBus;
        private readonly OrderAcceptedEventHandler _orderAcceptedEventHandler;
        private readonly OrderRejectedEventHandler _orderRejectedEventHandler;
        private readonly OrderCancelledEventHandler _orderCancelledEventHandler;
        private readonly OrderMatchEventHandler _orderMatchEventHandler;
        private readonly OrderCancelRejectedEventHandler _orderCancelRejectedEventHandler;

        public OrderManagementService(
            ILogger<OrderManagementService> logger,
            IOptions<ParallelismConfig> parallelismConfig,
            IOrderQueueConsumerFactory consumerFactory,
            IEventBus eventBus,
            IOrderRepository orderRepository,
            IAccountRepository accountRepository,
            ILoggerFactory loggerFactory)
        {
            _logger = logger;
            _parallelismConfig = parallelismConfig.Value;
            _consumerFactory = consumerFactory;
            _eventBus = eventBus;
            
            _orderAcceptedEventHandler = new OrderAcceptedEventHandler(orderRepository, _eventBus, loggerFactory.CreateLogger<OrderAcceptedEventHandler>());
            _orderRejectedEventHandler = new OrderRejectedEventHandler(orderRepository, _eventBus, loggerFactory.CreateLogger<OrderRejectedEventHandler>());
            _orderCancelledEventHandler = new OrderCancelledEventHandler(orderRepository, accountRepository, _eventBus, loggerFactory.CreateLogger<OrderCancelledEventHandler>());
            _orderMatchEventHandler = new OrderMatchEventHandler(orderRepository, accountRepository, _eventBus, loggerFactory.CreateLogger<OrderMatchEventHandler>());
            _orderCancelRejectedEventHandler = new OrderCancelRejectedEventHandler(orderRepository, _eventBus, loggerFactory.CreateLogger<OrderCancelRejectedEventHandler>());
        }

        public override async Task StartAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Starting OrderManagementService...");
            
            _eventBus.Subscribe(_orderAcceptedEventHandler);
            _eventBus.Subscribe(_orderRejectedEventHandler);
            _eventBus.Subscribe(_orderCancelledEventHandler);
            _eventBus.Subscribe(_orderMatchEventHandler);
            _eventBus.Subscribe(_orderCancelRejectedEventHandler);
            
            for (int i = 0; i < _parallelismConfig.PartitionCount; i++)
            {
                var shardId = i.ToString();
                var consumer = _consumerFactory.Create(shardId);
                _consumers.Add(consumer);
                
                _ = consumer.StartAsync(cancellationToken);
            }

            await base.StartAsync(cancellationToken);
        }

        public override async Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Stopping OrderManagementService...");
            
            _eventBus.Unsubscribe(_orderAcceptedEventHandler);
            _eventBus.Unsubscribe(_orderRejectedEventHandler);
            _eventBus.Unsubscribe(_orderCancelledEventHandler);
            _eventBus.Unsubscribe(_orderMatchEventHandler);
            _eventBus.Unsubscribe(_orderCancelRejectedEventHandler);

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
