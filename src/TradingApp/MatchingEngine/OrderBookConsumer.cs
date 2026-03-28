using Infrastructure.Queue;
using Microsoft.Extensions.Logging;

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

        public OrderBookConsumerFactory(
            ILoggerFactory loggerFactory,
            IOrderBookFactory orderBookFactory,
            [Microsoft.Extensions.DependencyInjection.FromKeyedServices("AggregatedCommandQueue")] IPartitionedMPSCQueueSystem<MatchingEngineCommand> commandInQueue)
        {
            _loggerFactory = loggerFactory;
            _orderBookFactory = orderBookFactory;
            _commandInQueue = commandInQueue;
        }

        public OrderBookConsumer Create(string symbol)
        {
            return new OrderBookConsumer(
                symbol,
                _orderBookFactory.GetOrderBook(symbol),
                _commandInQueue.GetQueue(symbol),
                _loggerFactory.CreateLogger<OrderBookConsumer>());
        }
    }

    public class OrderBookConsumer : IDisposable
    {
        private readonly string _symbol;
        private readonly OrderBook _orderBook;
        private readonly MPSCQueue<MatchingEngineCommand> _commandInQueue;
        private readonly ILogger<OrderBookConsumer> _logger;

        private Task? _executingTask;
        private CancellationTokenSource? _cts;

        public OrderBookConsumer(
            string symbol,
            OrderBook orderBook,
            MPSCQueue<MatchingEngineCommand> commandInQueue,
            ILogger<OrderBookConsumer> logger)
        {
            _symbol = symbol;
            _orderBook = orderBook;
            _commandInQueue = commandInQueue;
            _logger = logger;
        }

        public virtual Task StartAsync(CancellationToken cancellationToken)
        {
            _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            _executingTask = ExecuteAsync(_cts.Token);

            if (_executingTask.IsCompleted)
                return _executingTask;

            return Task.CompletedTask;
        }

        public virtual async Task StopAsync(CancellationToken cancellationToken)
        {
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
            return Task.Run(() =>
            {
                while (!stoppingToken.IsCancellationRequested)
                {
                    if (_commandInQueue.TryDequeue(out var command))
                    {
                        if (command.IsCancel && command.CancelRequest != null)
                        {
                            if (Guid.TryParse(command.CancelRequest.OrderId, out var orderId))
                            {
                                _orderBook.CancelOrder(orderId);
                            }
                        }
                        else if (!command.IsCancel && command.Order != null)
                        {
                            _orderBook.AddOrder(command.Order);
                        }
                    }
                    else
                    {
                        Thread.Yield();
                    }
                }
            }, stoppingToken);
        }
    }
}
