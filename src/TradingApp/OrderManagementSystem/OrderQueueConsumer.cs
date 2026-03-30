using Infrastructure.Queue;
using Infrastructure.Event;
using Model.Domain;
using Model.Event;
using Model.Request;
using Repository;

namespace OrderManagementSystem
{
    public interface IOrderQueueConsumerFactory
    {
        OrderQueueConsumer Create(string accountShardId);
    }

    public class OrderQueueConsumerFactory : IOrderQueueConsumerFactory
    {
        private readonly ILoggerFactory _loggerFactory;
        private readonly IOrderRepository _orderRepository;
        private readonly IEventBus _eventBus;
        private readonly IPartitionedMPSCQueueSystem<GatewayRequest> _requestInQueue;
        private readonly IPartitionedSPSCQueueSystem<Order> _orderOutQueue;
        private readonly IPartitionedMPSCQueueSystem<CancelOrderRequest> _cancelOutQueue;

        public OrderQueueConsumerFactory(
            ILoggerFactory loggerFactory,
            IOrderRepository orderRepository,
            IEventBus eventBus,
            [FromKeyedServices("AccountShardQueue1")] IPartitionedMPSCQueueSystem<GatewayRequest> requestInQueue,
            [FromKeyedServices("AccountShardQueue2")] IPartitionedSPSCQueueSystem<Order> orderOutQueue,
            [FromKeyedServices("CancelQueue")] IPartitionedMPSCQueueSystem<CancelOrderRequest> cancelOutQueue)
        {
            _loggerFactory = loggerFactory;
            _orderRepository = orderRepository;
            _eventBus = eventBus;
            _requestInQueue = requestInQueue;
            _orderOutQueue = orderOutQueue;
            _cancelOutQueue = cancelOutQueue;
        }

        public OrderQueueConsumer Create(string accountShardId)
        {
            return new OrderQueueConsumer(
                accountShardId,
                _orderRepository,
                _eventBus,
                _requestInQueue.GetQueue(accountShardId),
                _orderOutQueue.GetQueue(accountShardId),
                _cancelOutQueue, // Pass the entire queue system since keys belong to the symbol, not the consumer shard
                _loggerFactory.CreateLogger<OrderQueueConsumer>());
        }
    }

    public class OrderQueueConsumer
    {
        private readonly string _shardId;
        private readonly IOrderRepository _orderRepository;
        private readonly IEventBus _eventBus;
        private readonly MPSCQueue<GatewayRequest> _requestQueue;
        private readonly SPSCQueue<Order> _orderOutQueue;
        private readonly IPartitionedMPSCQueueSystem<CancelOrderRequest> _cancelOutQueueSystem;
        private readonly ILogger<OrderQueueConsumer> _logger;

        private Task? _executingTask;
        private CancellationTokenSource? _cts;

        public OrderQueueConsumer(
            string shardId,
            IOrderRepository orderRepository,
            IEventBus eventBus,
            MPSCQueue<GatewayRequest> requestQueue,
            SPSCQueue<Order> orderOutQueue,
            IPartitionedMPSCQueueSystem<CancelOrderRequest> cancelOutQueueSystem,
            ILogger<OrderQueueConsumer> logger)
        {
            _shardId = shardId;
            _orderRepository = orderRepository;
            _eventBus = eventBus;
            _requestQueue = requestQueue;
            _orderOutQueue = orderOutQueue;
            _cancelOutQueueSystem = cancelOutQueueSystem;
            _logger = logger;
        }

        public virtual Task StartAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Starting consumer for shard: {ShardId}", _shardId);
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

            _logger.LogInformation("Stopping consumer for shard: {ShardId}", _shardId);

            try
            {
                _cts?.Cancel();
            }
            finally
            {
                await Task.WhenAny(_executingTask, Task.Delay(Timeout.Infinite, cancellationToken));
            }
        }

        protected virtual Task ExecuteAsync(CancellationToken cancellationToken)
        {
            return Task.Run(async () =>
            {
                var idleDelayMs = 1;
                while (!cancellationToken.IsCancellationRequested)
                {
                    if (_requestQueue.TryDequeue(out var request))
                    {
                        idleDelayMs = 1;
                        await ProcessRequestAsync(request, cancellationToken);
                    }
                    else
                    {
                        await Task.Delay(idleDelayMs, cancellationToken);
                        if (idleDelayMs < 64)
                            idleDelayMs *= 2;
                    }
                }
            }, cancellationToken);
        }

        private async Task ProcessRequestAsync(GatewayRequest request, CancellationToken cancellationToken)
        {
            switch (request.Type)
            {
                case GatewayRequestType.PlaceOrder:
                    if (request.PlaceOrderRequest == null)
                    {
                        _logger.LogWarning("Received PlaceOrder request with null PlaceOrderRequest on shard {ShardId}", _shardId);
                        return;
                    }
                    await ProcessPlaceOrderRequestAsync(request.PlaceOrderRequest, cancellationToken);
                    break;
                case GatewayRequestType.CancelOrder:
                    if (request.CancelOrderRequest == null)
                    {
                        _logger.LogWarning("Received CancelOrder request with null CancelOrderRequest on shard {ShardId}", _shardId);
                        return;
                    }
                    await ProcessCancelOrderRequestAsync(request.CancelOrderRequest, cancellationToken);
                    break;
                default:
                    _logger.LogWarning("Unknown request type on shard {ShardId}: {RequestType}", _shardId, request.Type);
                    break;
            }
        }

        private async Task ProcessPlaceOrderRequestAsync(PlaceOrderRequest request, CancellationToken cancellationToken)
        {
            _logger.LogInformation("Processing PlaceOrder request for shard {ShardId}, Symbol {Symbol}: AccountKey={AccountKey}, Quantity={Quantity}, Price={Price}, Side={Side}",
                _shardId, request.Symbol, request.AccountKey, request.Quantity, request.Price, request.Side);
            var fsm = new OrderFSM();
            var order = new Order
            {
                OrderId = Guid.NewGuid(),
                AccountKey = request.AccountKey,
                Status = fsm.CurrentState,
                Symbol = request.Symbol,
                TotalQuantity = request.Quantity,
                FilledQuantity = 0,
                Price = request.Price,
                Side = request.Side
            };
            _orderRepository.TryAdd(order);
            _eventBus.Publish(new OrderUpdateEvent { Order = order, Remark = "Order Created" });

            await EnqueueWithBackoffAsync(() => _orderOutQueue.TryEnqueue(order), cancellationToken);

            _logger.LogInformation("Order placed successfully for shard {ShardId}, Symbol {Symbol}: AccountKey={AccountKey}, Quantity={Quantity}, Price={Price}, Side={Side}",
                _shardId, request.Symbol, request.AccountKey, request.Quantity, request.Price, request.Side);
        }

        private async Task ProcessCancelOrderRequestAsync(CancelOrderRequest request, CancellationToken cancellationToken)
        {
            if (!_orderRepository.TryGet(request.OrderId, out var order))
            {
                _logger.LogWarning("Cannot process CancelOrder request for shard {ShardId}, unknown OrderId={OrderId}", _shardId, request.OrderId);
                return;
            }

            _logger.LogInformation("Processing CancelOrder request for shard {ShardId}, Symbol {Symbol}: OrderId={OrderId}", _shardId, order.Symbol, request.OrderId);

            var fsm = new OrderFSM(order.Status);
            if (fsm.ProcessEvent(OrderEvent.CancelRequest))
            {
                order.Status = fsm.CurrentState;
                _orderRepository.AddOrUpdate(order);
                _eventBus.Publish(new OrderUpdateEvent { Order = order, Remark = "Pending Cancel" });
            }

            var queue = _cancelOutQueueSystem.GetQueue(order.Symbol);

            await EnqueueWithBackoffAsync(() => queue.TryEnqueue(request), cancellationToken);
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
    }
}
