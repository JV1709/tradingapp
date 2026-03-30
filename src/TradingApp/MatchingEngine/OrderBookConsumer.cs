using Infrastructure.Queue;
using Infrastructure.Event;
using Model.Event;

namespace MatchingEngine
{
    public interface IOrderBookConsumerFactory
    {
        OrderBookConsumer Create(string symbol);
    }

    public class OrderBookConsumerFactory : IOrderBookConsumerFactory
    {
        private readonly ILoggerFactory _loggerFactory;
        private readonly IOrderBookFactory _orderBookFactory;
        private readonly IPartitionedMPSCQueueSystem<MatchingEngineCommand> _commandInQueue;
        private readonly IEventBus _eventBus;

        public OrderBookConsumerFactory(
            ILoggerFactory loggerFactory,
            IOrderBookFactory orderBookFactory,
            IEventBus eventBus,
            [FromKeyedServices("AggregatedCommandQueue")] IPartitionedMPSCQueueSystem<MatchingEngineCommand> commandInQueue)
        {
            _loggerFactory = loggerFactory;
            _orderBookFactory = orderBookFactory;
            _commandInQueue = commandInQueue;
            _eventBus = eventBus;
        }

        public OrderBookConsumer Create(string symbol)
        {
            return new OrderBookConsumer(
                symbol,
                _orderBookFactory.GetOrderBook(symbol),
                _commandInQueue.GetQueue(symbol),
                _loggerFactory.CreateLogger<OrderBookConsumer>(),
                _eventBus);
        }
    }

    public class OrderBookConsumer : IDisposable
    {
        private readonly string _symbol;
        private readonly OrderBook _orderBook;
        private readonly MPSCQueue<MatchingEngineCommand> _commandInQueue;
        private readonly ILogger<OrderBookConsumer> _logger;
        private readonly IEventBus _eventBus;

        private Task? _executingTask;
        private CancellationTokenSource? _cts;

        public OrderBookConsumer(
            string symbol,
            OrderBook orderBook,
            MPSCQueue<MatchingEngineCommand> commandInQueue,
            ILogger<OrderBookConsumer> logger,
            IEventBus eventBus)
        {
            _symbol = symbol;
            _orderBook = orderBook;
            _commandInQueue = commandInQueue;
            _logger = logger;
            _eventBus = eventBus;
        }

        public virtual Task StartAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("OrderBookConsumer for {Symbol} starting...", _symbol);
            _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            _executingTask = ExecuteAsync(_cts.Token);

            if (_executingTask.IsCompleted)
                return _executingTask;

            return Task.CompletedTask;
        }

        public virtual async Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("OrderBookConsumer for {Symbol} stopping...", _symbol);
            if (_executingTask == null) return;
            try
            {
                _cts?.Cancel();
            }
            finally
            {
                await Task.WhenAny(_executingTask, Task.Delay(Timeout.Infinite, cancellationToken));
            }
        }

        public virtual void Dispose()
        {
            _cts?.Cancel();
            _cts?.Dispose();
        }

        protected virtual Task ExecuteAsync(CancellationToken stoppingToken)
        {
            return Task.Run(async () =>
            {
                _logger.LogInformation("OrderBookConsumer execution loop started for {Symbol}.", _symbol);
                var idleDelayMs = 1;
                while (!stoppingToken.IsCancellationRequested)
                {
                    if (_commandInQueue.TryDequeue(out var command))
                    {
                        idleDelayMs = 1;
                        _logger.LogInformation("Dequeued command for {Symbol}: IsCancel={IsCancel}, OrderId={OrderId}",
                            _symbol, command.IsCancel, command.IsCancel ? command.CancelRequest?.OrderId.ToString() : command.Order?.OrderId.ToString());
                        if (command.IsCancel && command.CancelRequest != null)
                        {
                            var orderId = command.CancelRequest.OrderId;
                            _logger.LogInformation("Processing cancel for order {OrderId}", orderId);
                            _orderBook.CancelOrder(orderId);
                            _eventBus.Publish(new OrderCancelledEvent { OrderId = orderId });
                        }
                        else if (!command.IsCancel && command.Order != null)
                        {
                            _logger.LogInformation("Processing add order for {OrderId}", command.Order.OrderId);
                            var match = _orderBook.AddOrder(command.Order);
                            if (match != null)
                            {
                                _logger.LogInformation("A match occurred for order {OrderId}", command.Order.OrderId);

                                var buyOrderId = command.Order.Side == Model.Domain.Side.Buy ? match.TakerOrderId : match.MakerOrderId;
                                var sellOrderId = command.Order.Side == Model.Domain.Side.Sell ? match.TakerOrderId : match.MakerOrderId;

                                _eventBus.Publish(new OrderMatchEvent
                                {
                                    BuyOrderId = buyOrderId,
                                    SellOrderId = sellOrderId,
                                    FillQuantity = match.Quantity,
                                    FillPrice = match.Price,
                                    BidPrice = match.BidPrice,
                                    AskPrice = match.AskPrice
                                });
                            }
                            else
                            {
                                _logger.LogInformation("No match for order {OrderId}, added to order book.", command.Order.OrderId);
                                _eventBus.Publish(new OrderAcceptedEvent { OrderId = command.Order.OrderId });
                            }
                        }
                    }
                    else
                    {
                        await Task.Delay(idleDelayMs, stoppingToken);
                        if (idleDelayMs < 64)
                            idleDelayMs *= 2;
                    }
                }
                _logger.LogInformation("OrderBookConsumer execution loop finished for {Symbol}.", _symbol);
            }, stoppingToken);
        }
    }
}
