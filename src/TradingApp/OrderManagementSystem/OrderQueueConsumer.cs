using Infrastructure.Queue;
using Model.Domain;
using Model.Request;
using Repository;

namespace OrderManagementSystem
{
    public interface IOrderQueueConsumerFactory
    {
        OrderQueueConsumer Create(string symbol);
    }

    public class OrderQueueConsumerFactory : IOrderQueueConsumerFactory
    {
        private readonly ILoggerFactory _loggerFactory;
        private readonly IOrderRepository _orderRepository;
        private readonly IPartitionedMPSCQueueSystem<GatewayRequest> _requestInQueue;
        private readonly IPartitionedSPSCQueueSystem<Order> _orderOutQueue;
        private readonly IPartitionedMPSCQueueSystem<CancelOrderRequest> _cancelOutQueue;

        public OrderQueueConsumerFactory(
            ILoggerFactory loggerFactory,
            IOrderRepository orderRepository,
            [FromKeyedServices("AccountShardQueue1")] IPartitionedMPSCQueueSystem<GatewayRequest> requestInQueue,
            [FromKeyedServices("AccountShardQueue2")] IPartitionedSPSCQueueSystem<Order> orderOutQueue,
            [FromKeyedServices("CancelQueue")] IPartitionedMPSCQueueSystem<CancelOrderRequest> cancelOutQueue)
        {
            _loggerFactory = loggerFactory;
            _orderRepository = orderRepository;
            _requestInQueue = requestInQueue;
            _orderOutQueue = orderOutQueue;
            _cancelOutQueue = cancelOutQueue;
        }

        public OrderQueueConsumer Create(string symbol)
        {
            return new OrderQueueConsumer(
                symbol,
                _orderRepository,
                _requestInQueue.GetQueue(symbol),
                _orderOutQueue.GetQueue(symbol),
                _cancelOutQueue.GetQueue(symbol),
                _loggerFactory.CreateLogger<OrderQueueConsumer>());
        }
    }

    public class OrderQueueConsumer
    {
        private readonly string _symbol;
        private readonly IOrderRepository _orderRepository;
        private readonly MPSCQueue<GatewayRequest> _requestQueue;
        private readonly SPSCQueue<Order> _orderOutQueue;
        private readonly MPSCQueue<CancelOrderRequest> _cancelOutQueue;
        private readonly ILogger<OrderQueueConsumer> _logger;

        private Task? _executingTask;
        private CancellationTokenSource? _cts;

        public OrderQueueConsumer(
            string symbol,
            IOrderRepository orderRepository,
            MPSCQueue<GatewayRequest> requestQueue,
            SPSCQueue<Order> orderOutQueue,
            MPSCQueue<CancelOrderRequest> cancelOutQueue,
            ILogger<OrderQueueConsumer> logger)
        {
            _symbol = symbol;
            _orderRepository = orderRepository;
            _requestQueue = requestQueue;
            _orderOutQueue = orderOutQueue;
            _cancelOutQueue = cancelOutQueue;
            _logger = logger;
        }

        public virtual Task StartAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Starting consumer for symbol: {Symbol}", _symbol);
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

            _logger.LogInformation("Stopping consumer for symbol: {Symbol}", _symbol);

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
            return Task.Run(() =>
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    if (_requestQueue.TryDequeue(out var request))
                    {
                        ProcessRequest(request, cancellationToken);
                    }
                    else
                    {
                        Thread.Yield();
                    }
                }
            }, cancellationToken);
        }

        private void ProcessRequest(GatewayRequest request, CancellationToken cancellationToken)
        {
            switch (request.Type)
            {
                case GatewayRequestType.PlaceOrder:
                    if (request.PlaceOrderRequest == null)
                    {
                        _logger.LogWarning("Received PlaceOrder request with null PlaceOrderRequest for symbol {Symbol}", _symbol);
                        return;
                    }
                    ProcessPlaceOrderRequest(request.PlaceOrderRequest, cancellationToken);
                    break;
                case GatewayRequestType.CancelOrder:
                    if (request.CancelOrderRequest == null)
                    {
                        _logger.LogWarning("Received CancelOrder request with null CancelOrderRequest for symbol {Symbol}", _symbol);
                        return;
                    }
                    ProcessCancelOrderRequest(request.CancelOrderRequest, cancellationToken);
                    break;
                default:
                    _logger.LogWarning("Unknown request type for symbol {Symbol}: {RequestType}", _symbol, request.Type);
                    break;
            }
        }

        private void ProcessPlaceOrderRequest(PlaceOrderRequest request, CancellationToken cancellationToken)
        {
            _logger.LogInformation("Processing PlaceOrder request for symbol {Symbol}: AccountKey={AccountKey}, Quantity={Quantity}, Price={Price}, Side={Side}",
                _symbol, request.AccountKey, request.Quantity, request.Price, request.Side);
            var order = new Order
            {
                OrderId = Guid.NewGuid(),
                AccountKey = request.AccountKey,
                Status = OrderStatus.New,
                Symbol = _symbol,
                TotalQuantity = request.Quantity,
                FilledQuantity = 0,
                Price = request.Price,
                Side = request.Side
            };

            while (!_orderOutQueue.TryEnqueue(order) && !cancellationToken.IsCancellationRequested)
                Task.Delay(1, cancellationToken);

            _logger.LogInformation("Order placed successfully for symbol {Symbol}: AccountKey={AccountKey}, Quantity={Quantity}, Price={Price}, Side={Side}",
                _symbol, request.AccountKey, request.Quantity, request.Price, request.Side);
        }

        private void ProcessCancelOrderRequest(CancelOrderRequest request, CancellationToken cancellationToken)
        {
            _logger.LogInformation("Processing CancelOrder request for symbol {Symbol}: OrderId={OrderId}", _symbol, request.OrderId);
            while (!_cancelOutQueue.TryEnqueue(request) && !cancellationToken.IsCancellationRequested)
                Task.Delay(1, cancellationToken);
        }
    }
}
