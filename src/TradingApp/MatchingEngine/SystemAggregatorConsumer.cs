using Infrastructure.Queue;
using Model.Domain;
using Model.Request;

namespace MatchingEngine
{
    public interface ISystemAggregatorConsumerFactory
    {
        SystemAggregatorConsumer Create(string symbol);
    }

    public class SystemAggregatorConsumerFactory : ISystemAggregatorConsumerFactory
    {
        private readonly ILoggerFactory _loggerFactory;
        private readonly IPartitionedMPSCQueueSystem<CancelOrderRequest> _cancelInQueue;
        private readonly IPartitionedMPSCQueueSystem<Order> _orderInQueue;
        private readonly IPartitionedMPSCQueueSystem<MatchingEngineCommand> _commandOutQueue;

        public SystemAggregatorConsumerFactory(
            ILoggerFactory loggerFactory,
            [FromKeyedServices("CancelQueue")] IPartitionedMPSCQueueSystem<CancelOrderRequest> cancelInQueue,
            [FromKeyedServices("InstrumentQueue")] IPartitionedMPSCQueueSystem<Order> orderInQueue,
            [FromKeyedServices("AggregatedCommandQueue")] IPartitionedMPSCQueueSystem<MatchingEngineCommand> commandOutQueue)
        {
            _loggerFactory = loggerFactory;
            _cancelInQueue = cancelInQueue;
            _orderInQueue = orderInQueue;
            _commandOutQueue = commandOutQueue;
        }

        public SystemAggregatorConsumer Create(string symbol)
        {
            return new SystemAggregatorConsumer(
                symbol,
                _cancelInQueue.GetQueue(symbol),
                _orderInQueue.GetQueue(symbol),
                _commandOutQueue.GetQueue(symbol),
                _loggerFactory.CreateLogger<SystemAggregatorConsumer>());
        }
    }

    public class SystemAggregatorConsumer : IDisposable
    {
        private readonly string _symbol;
        private readonly MPSCQueue<CancelOrderRequest> _cancelInQueue;
        private readonly MPSCQueue<Order> _orderInQueue;
        private readonly MPSCQueue<MatchingEngineCommand> _commandOutQueue;
        private readonly ILogger<SystemAggregatorConsumer> _logger;

        private Task? _executingTask;
        private CancellationTokenSource? _cts;

        public SystemAggregatorConsumer(
            string symbol,
            MPSCQueue<CancelOrderRequest> cancelInQueue,
            MPSCQueue<Order> orderInQueue,
            MPSCQueue<MatchingEngineCommand> commandOutQueue,
            ILogger<SystemAggregatorConsumer> logger)
        {
            _symbol = symbol;
            _cancelInQueue = cancelInQueue;
            _orderInQueue = orderInQueue;
            _commandOutQueue = commandOutQueue;
            _logger = logger;
        }

        public virtual Task StartAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("SystemAggregatorConsumer for {Symbol} starting...", _symbol);
            _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            _executingTask = ExecuteAsync(_cts.Token);

            if (_executingTask.IsCompleted)
                return _executingTask;

            return Task.CompletedTask;
        }

        public virtual async Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("SystemAggregatorConsumer stopping...");
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
                _logger.LogInformation("SystemAggregatorConsumer execution loop started.");
                while (!stoppingToken.IsCancellationRequested)
                {
                    bool workDone = false;

                    // Round-robin polling: Cancel queue gets slight priority/even turn
                    if (_cancelInQueue.TryDequeue(out var cancelReq))
                    {
                        _logger.LogInformation("Dequeued CancelOrderRequest for OrderId {OrderId} from CancelQueue.", cancelReq.OrderId);
                        var cmd = MatchingEngineCommand.CreateCancelOrder(cancelReq);
                        while (!_commandOutQueue.TryEnqueue(cmd) && !stoppingToken.IsCancellationRequested)
                            await Task.Delay(1, stoppingToken);
                        _logger.LogInformation("Enqueued MatchingEngineCommand for CancelOrderRequest of OrderId {OrderId} to AggregatedCommandQueue.", cancelReq.OrderId);

                        workDone = true;
                    }

                    if (_orderInQueue.TryDequeue(out var order))
                    {
                        _logger.LogInformation("Dequeued Order {OrderId} from InstrumentQueue.", order.OrderId);
                        var cmd = MatchingEngineCommand.CreateAddOrder(order);
                        while (!_commandOutQueue.TryEnqueue(cmd) && !stoppingToken.IsCancellationRequested)
                            await Task.Delay(1, stoppingToken);
                        _logger.LogInformation("Enqueued MatchingEngineCommand for OrderId {OrderId} to AggregatedCommandQueue.", order.OrderId);

                        workDone = true;
                    }

                    if (!workDone)
                    {
                        await Task.Delay(1, stoppingToken);
                    }
                }
                _logger.LogInformation("SystemAggregatorConsumer execution loop finished.");
            }, stoppingToken);
        }
    }
}
