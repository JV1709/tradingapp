using Infrastructure.Event;
using Infrastructure.Queue;
using Model.Domain;
using Model.Event;
using Repository;

namespace RiskManagementSystem
{
    public interface IRiskManagementConsumerFactory
    {
        RiskManagementConsumer Create(int partitionId);
    }

    public class RiskManagementConsumerFactory : IRiskManagementConsumerFactory
    {
        private readonly ILoggerFactory _loggerFactory;
        private readonly IAccountRepository _accountRepository;
        private readonly IEventBus _eventBus;
        private readonly IPartitionedSPSCQueueSystem<Order> _orderInQueue;
        private readonly IPartitionedMPSCQueueSystem<Order> _orderOutQueue;

        public RiskManagementConsumerFactory(
            ILoggerFactory loggerFactory,
            IAccountRepository accountRepository,
            IEventBus eventBus,
            [FromKeyedServices("AccountShardQueue2")] IPartitionedSPSCQueueSystem<Order> orderInQueue,
            [FromKeyedServices("InstrumentQueue")] IPartitionedMPSCQueueSystem<Order> orderOutQueue)
        {
            _loggerFactory = loggerFactory;
            _accountRepository = accountRepository;
            _eventBus = eventBus;
            _orderInQueue = orderInQueue;
            _orderOutQueue = orderOutQueue;
        }

        public RiskManagementConsumer Create(int partitionId)
        {
            var partitionIdStr = partitionId.ToString();
            return new RiskManagementConsumer(
                partitionId,
                _orderInQueue.GetQueue(partitionIdStr),
                _orderOutQueue,
                _accountRepository, 
                _eventBus,
                _loggerFactory.CreateLogger<RiskManagementConsumer>());
        }
    }

    public class RiskManagementConsumer : IDisposable
    {
        private readonly int _partitionId;
        private readonly SPSCQueue<Order> _orderInQueue;
        private readonly IPartitionedMPSCQueueSystem<Order> _orderOutQueue;
        private readonly IAccountRepository _accountRepository;
        private readonly IEventBus _eventBus;
        private readonly ILogger<RiskManagementConsumer> _logger;

        private Task? _executingTask;
        private CancellationTokenSource? _cts;

        public RiskManagementConsumer(
            int partitionId,
            SPSCQueue<Order> orderInQueue,
            IPartitionedMPSCQueueSystem<Order> orderOutQueue,
            IAccountRepository accountRepository,
            IEventBus eventBus,
            ILogger<RiskManagementConsumer> logger)
        {
            _partitionId = partitionId;
            _orderInQueue = orderInQueue;
            _orderOutQueue = orderOutQueue;
            _accountRepository = accountRepository;
            _eventBus = eventBus;
            _logger = logger;
        }

        public virtual Task StartAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Starting consumer for partition: {PartitionId}", _partitionId);
            _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            _executingTask = ExecuteAsync(_cts.Token);

            if (_executingTask.IsCompleted)
                return _executingTask;

            return Task.CompletedTask;
        }

        public virtual async Task StopAsync(CancellationToken cancellationToken)
        {
            if (_executingTask == null)
            {
                return;
            }

            _logger.LogInformation("Stopping consumer for partition: {PartitionId}", _partitionId);

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
                var idleDelayMs = 1;
                while (!stoppingToken.IsCancellationRequested)
                {
                    if (_orderInQueue.TryDequeue(out var order))
                    {
                        idleDelayMs = 1;
                        var (isValid, message) = ValidateOrder(order);
                        if (!isValid)
                        {
                            _logger.LogWarning("Order validation failed for partition: {PartitionId}, reason: {Reason}", _partitionId, message);
                            _eventBus.Publish(new OrderRejectedEvent
                            {
                                OrderId = order.OrderId,
                                RejectionReason = message
                            });
                            continue;
                        }

                        if (!_accountRepository.TryGet(order.AccountKey, out var account))
                        {
                            _logger.LogWarning("Account not found while reserving for partition: {PartitionId}, account: {AccountKey}", _partitionId, order.AccountKey);
                            _eventBus.Publish(new OrderRejectedEvent
                            {
                                OrderId = order.OrderId,
                                RejectionReason = "Account not found"
                            });
                            continue;
                        }
                        if (order.Side == Side.Buy)
                        {
                            account.AvailableBalance -= order.Price * order.TotalQuantity;
                        }
                        else
                        {
                            var holding = account.Holdings.FirstOrDefault(h => string.Equals(h.Symbol, order.Symbol, StringComparison.OrdinalIgnoreCase));
                            if (holding == null || holding.AvailableQuantity < order.TotalQuantity)
                            {
                                _logger.LogWarning("Insufficient available holdings while reserving for partition: {PartitionId}, account: {AccountKey}, symbol: {Symbol}", _partitionId, order.AccountKey, order.Symbol);
                                _eventBus.Publish(new OrderRejectedEvent
                                {
                                    OrderId = order.OrderId,
                                    RejectionReason = "Insufficient available holdings"
                                });
                                continue;
                            }

                            holding.AvailableQuantity -= order.TotalQuantity;
                        }
                        _accountRepository.AddOrUpdate(account);

                        _eventBus.Publish(new AccountUpdateEvent
                        {
                            Username = account.Username,
                            TotalBalance = account.TotalBalance,
                            AvailableBalance = account.AvailableBalance,
                            Holdings = account.Holdings
                        });

                        var queue = _orderOutQueue.GetQueue(order.Symbol);
                        await EnqueueWithBackoffAsync(() => queue.TryEnqueue(order), stoppingToken);
                        _logger.LogInformation("Order enqueued to output queue for partition: {PartitionId}", _partitionId);
                    }
                    else
                    {
                        await Task.Delay(idleDelayMs, stoppingToken);
                        if (idleDelayMs < 64)
                            idleDelayMs *= 2;
                    }
                }
            }, stoppingToken);
        }

        private static async Task EnqueueWithBackoffAsync(Func<bool> tryEnqueue, CancellationToken cancellationToken)
        {
            var delayMs = 1;
            while (!tryEnqueue() && !cancellationToken.IsCancellationRequested)
            {
                await Task.Delay(delayMs, cancellationToken);
                if (delayMs < 64)
                    delayMs *= 2;
            }
        }

        private (bool IsValid, string Message) ValidateOrder(Order order)
        {
            if (order.TotalQuantity <= 0)
                return (false, "Invalid quantity");

            if (order.Price <= 0)
                return (false, "Invalid price");

            if (!_accountRepository.TryGet(order.AccountKey, out var account))
                return (false, "Account not found");

            if (order.Side == Side.Buy)
            {
                if (order.Price * order.TotalQuantity > account.AvailableBalance)
                    return (false, "Insufficient funds");
            }

            if (order.Side == Side.Sell)
            {
                var holding = account.Holdings.FirstOrDefault(h => string.Equals(h.Symbol, order.Symbol, StringComparison.OrdinalIgnoreCase));
                if (holding == null || holding.AvailableQuantity < order.TotalQuantity)
                    return (false, "Insufficient available holdings");
            }

            return (true, string.Empty);
        }
    }
}
