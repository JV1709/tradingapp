using Infrastructure.Event;
using Model.Domain;
using Model.Event;
using Repository;

namespace OrderManagementSystem
{
    public class OrderAcceptedEventHandler : IEventHandler<OrderAcceptedEvent>
    {
        private readonly IOrderRepository _orderRepository;
        private readonly IEventBus _eventBus;
        private readonly ILogger<OrderAcceptedEventHandler> _logger;

        public OrderAcceptedEventHandler(IOrderRepository orderRepository, IEventBus eventBus, ILogger<OrderAcceptedEventHandler> logger)
        {
            _orderRepository = orderRepository;
            _eventBus = eventBus;
            _logger = logger;
        }

        public Task HandleAsync(OrderAcceptedEvent @event, CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("Handling OrderAcceptedEvent for OrderId: {OrderId}", @event.OrderId);
            if (_orderRepository.TryGet(@event.OrderId, out var order))
            {
                var fsm = new OrderFSM(order.Status);
                if (fsm.ProcessEvent(OrderEvent.Accepted))
                {
                    order.Status = fsm.CurrentState;
                    _orderRepository.AddOrUpdate(order);
                    _eventBus.Publish(new OrderUpdateEvent { Order = order, Remark = "Accepted" });
                    _logger.LogInformation("Order {OrderId} status updated to {Status}.", @event.OrderId, order.Status);
                }
            }
            else
            {
                _logger.LogWarning("Order {OrderId} not found.", @event.OrderId);
            }
            return Task.CompletedTask;
        }
    }

    public class OrderRejectedEventHandler : IEventHandler<OrderRejectedEvent>
    {
        private readonly IOrderRepository _orderRepository;
        private readonly IEventBus _eventBus;
        private readonly ILogger<OrderRejectedEventHandler> _logger;

        public OrderRejectedEventHandler(IOrderRepository orderRepository, IEventBus eventBus, ILogger<OrderRejectedEventHandler> logger)
        {
            _orderRepository = orderRepository;
            _eventBus = eventBus;
            _logger = logger;
        }

        public Task HandleAsync(OrderRejectedEvent @event, CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("Handling OrderRejectedEvent for OrderId: {OrderId}", @event.OrderId);
            if (_orderRepository.TryGet(@event.OrderId, out var order))
            {
                var fsm = new OrderFSM(order.Status);
                if (fsm.ProcessEvent(OrderEvent.Rejected))
                {
                    order.Status = fsm.CurrentState;
                    _orderRepository.AddOrUpdate(order);
                    _eventBus.Publish(new OrderUpdateEvent { Order = order, Remark = "Rejected: " + @event.RejectionReason });
                    _logger.LogInformation("Order {OrderId} status updated to {Status}. Reason: {RejectionReason}", @event.OrderId, order.Status, @event.RejectionReason);
                }
            }
            else
            {
                _logger.LogWarning("Order {OrderId} not found.", @event.OrderId);
            }
            return Task.CompletedTask;
        }
    }

    public class OrderCancelledEventHandler : IEventHandler<OrderCancelledEvent>
    {
        private readonly IOrderRepository _orderRepository;
        private readonly IAccountRepository _accountRepository;
        private readonly IEventBus _eventBus;
        private readonly ILogger<OrderCancelledEventHandler> _logger;

        public OrderCancelledEventHandler(IOrderRepository orderRepository, IAccountRepository accountRepository, IEventBus eventBus, ILogger<OrderCancelledEventHandler> logger)
        {
            _orderRepository = orderRepository;
            _accountRepository = accountRepository;
            _eventBus = eventBus;
            _logger = logger;
        }

        public Task HandleAsync(OrderCancelledEvent @event, CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("Handling OrderCancelledEvent for OrderId: {OrderId}", @event.OrderId);
            if (_orderRepository.TryGet(@event.OrderId, out var order))
            {
                var fsm = new OrderFSM(order.Status);
                if (fsm.ProcessEvent(OrderEvent.Cancelled))
                {
                    order.Status = fsm.CurrentState;
                    _orderRepository.AddOrUpdate(order);
                    _eventBus.Publish(new OrderUpdateEvent { Order = order, Remark = "Cancelled" });
                    ReleaseReservedResources(order);
                    _logger.LogInformation("Order {OrderId} status updated to {Status}.", @event.OrderId, order.Status);
                }
            }
            else
            {
                _logger.LogWarning("Order {OrderId} not found.", @event.OrderId);
            }
            return Task.CompletedTask;
        }

        private void ReleaseReservedResources(Order order)
        {
            if (!_accountRepository.TryGet(order.AccountKey, out var account))
            {
                _logger.LogWarning("Account {AccountKey} not found while releasing reserved resources for OrderId {OrderId}.", order.AccountKey, order.OrderId);
                return;
            }

            var remainingQuantity = Math.Max(0L, order.TotalQuantity - order.FilledQuantity);

            if (order.Side == Side.Buy)
            {
                var releasedAmount = order.Price * (decimal)remainingQuantity;
                account.AvailableBalance = Math.Min(account.TotalBalance, account.AvailableBalance + releasedAmount);
            }

            if (order.Side == Side.Sell)
            {
                var holding = account.Holdings.FirstOrDefault(h => string.Equals(h.Symbol, order.Symbol, StringComparison.OrdinalIgnoreCase));
                if (holding != null)
                {
                    holding.AvailableQuantity = holding.AvailableQuantity + remainingQuantity;
                }
            }

            _accountRepository.AddOrUpdate(account);
            _eventBus.Publish(new AccountUpdateEvent
            {
                Username = account.Username,
                TotalBalance = account.TotalBalance,
                AvailableBalance = account.AvailableBalance,
                Holdings = account.Holdings
            });
        }
    }

    public class OrderMatchEventHandler : IEventHandler<OrderMatchEvent>
    {
        private readonly IOrderRepository _orderRepository;
        private readonly IAccountRepository _accountRepository;
        private readonly IEventBus _eventBus;
        private readonly ILogger<OrderMatchEventHandler> _logger;

        public OrderMatchEventHandler(IOrderRepository orderRepository, IAccountRepository accountRepository, IEventBus eventBus, ILogger<OrderMatchEventHandler> logger)
        {
            _orderRepository = orderRepository;
            _accountRepository = accountRepository;
            _eventBus = eventBus;
            _logger = logger;
        }

        public Task HandleAsync(OrderMatchEvent @event, CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("Handling OrderMatchEvent for BuyOrderId: {BuyOrderId}, SellOrderId: {SellOrderId}", @event.BuyOrderId, @event.SellOrderId);
            ProcessOrder(@event.BuyOrderId, @event.FillQuantity, @event.FillPrice);
            ProcessOrder(@event.SellOrderId, @event.FillQuantity, @event.FillPrice);
            return Task.CompletedTask;
        }

        private void ProcessOrder(Guid orderId, long filledQuantity, decimal fillPrice)
        {
            if (_orderRepository.TryGet(orderId, out var order))
            {
                var remainingQuantity = Math.Max(0L, order.TotalQuantity - order.FilledQuantity);
                var matchedQuantity = Math.Min(filledQuantity, remainingQuantity);

                if (matchedQuantity <= 0)
                {
                    return;
                }

                var previousFilledQuantity = order.FilledQuantity;
                var newFilledQuantity = previousFilledQuantity + matchedQuantity;

                var previousNotional = order.AverageFillPrice * previousFilledQuantity;
                var matchedNotional = fillPrice * matchedQuantity;
                order.AverageFillPrice = (previousNotional + matchedNotional) / newFilledQuantity;

                order.FilledQuantity += matchedQuantity;
                var fsm = new OrderFSM(order.Status);
                var isFill = order.FilledQuantity >= order.TotalQuantity;
                var orderEvent = isFill ? OrderEvent.Fill : OrderEvent.PartialFill;

                if (fsm.ProcessEvent(orderEvent))
                {
                    order.Status = fsm.CurrentState;
                    _orderRepository.AddOrUpdate(order);
                    _eventBus.Publish(new OrderUpdateEvent { Order = order, Remark = "Matched" });
                    _logger.LogInformation("Order {OrderId} status updated to {Status}. FilledQuantity: {FilledQuantity}", orderId, order.Status, order.FilledQuantity);
                }
                else
                {
                    _orderRepository.AddOrUpdate(order); // Just update the quantity if state doesn't change
                    _eventBus.Publish(new OrderUpdateEvent { Order = order, Remark = "Match Quantity Updated" });
                }

                UpdateAccountForMatch(order, matchedQuantity, fillPrice);
            }
            else
            {
                _logger.LogWarning("Order {OrderId} not found.", orderId);
            }
        }

        private void UpdateAccountForMatch(Order order, long matchedQuantity, decimal fillPrice)
        {
            if (!_accountRepository.TryGet(order.AccountKey, out var account))
            {
                _logger.LogWarning("Account {AccountKey} not found while applying match for OrderId {OrderId}.", order.AccountKey, order.OrderId);
                return;
            }

            if (order.Side == Side.Buy)
            {
                var reservedAmount = order.Price * (decimal)matchedQuantity;
                var tradedAmount = fillPrice * (decimal)matchedQuantity;
                var refund = Math.Max(0, reservedAmount - tradedAmount);

                account.TotalBalance -= tradedAmount;
                account.AvailableBalance = Math.Min(account.TotalBalance, account.AvailableBalance + refund);

                var holding = account.Holdings.FirstOrDefault(h => string.Equals(h.Symbol, order.Symbol, StringComparison.OrdinalIgnoreCase));
                if (holding == null)
                {
                    account.Holdings.Add(new Holding { Symbol = order.Symbol, TotalQuantity = matchedQuantity, AvailableQuantity = matchedQuantity });
                }
                else
                {
                    holding.TotalQuantity += matchedQuantity;
                    holding.AvailableQuantity += matchedQuantity;
                }
            }
            else
            {
                var tradedAmount = fillPrice * (decimal)matchedQuantity;
                account.TotalBalance += tradedAmount;
                account.AvailableBalance += tradedAmount;

                var holding = account.Holdings.FirstOrDefault(h => string.Equals(h.Symbol, order.Symbol, StringComparison.OrdinalIgnoreCase));
                if (holding == null)
                {
                    _logger.LogWarning("No holding found for sell order. Account {AccountKey}, Symbol {Symbol}, OrderId {OrderId}", order.AccountKey, order.Symbol, order.OrderId);
                }
                else
                {
                    holding.TotalQuantity = Math.Max(0, holding.TotalQuantity - matchedQuantity);
                    holding.AvailableQuantity = Math.Min(holding.AvailableQuantity, holding.TotalQuantity);
                    if (holding.TotalQuantity == 0)
                    {
                        account.Holdings.Remove(holding);
                    }
                }
            }

            _accountRepository.AddOrUpdate(account);
            _eventBus.Publish(new AccountUpdateEvent
            {
                Username = account.Username,
                TotalBalance = account.TotalBalance,
                AvailableBalance = account.AvailableBalance,
                Holdings = account.Holdings
            });
        }
    }

    public class OrderCancelRejectedEventHandler : IEventHandler<OrderCancelRejectedEvent>
    {
        private readonly IOrderRepository _orderRepository;
        private readonly IEventBus _eventBus;
        private readonly ILogger<OrderCancelRejectedEventHandler> _logger;

        public OrderCancelRejectedEventHandler(IOrderRepository orderRepository, IEventBus eventBus, ILogger<OrderCancelRejectedEventHandler> logger)
        {
            _orderRepository = orderRepository;
            _eventBus = eventBus;
            _logger = logger;
        }

        public Task HandleAsync(OrderCancelRejectedEvent @event, CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("Handling OrderCancelRejectedEvent for OrderId: {OrderId}", @event.OrderId);
            if (_orderRepository.TryGet(@event.OrderId, out var order))
            {
                if (order.Status == OrderStatus.PendingCancel)
                {
                    order.Status = order.FilledQuantity > 0 ? OrderStatus.PartiallyFilled : OrderStatus.New;
                    _orderRepository.AddOrUpdate(order);
                    _eventBus.Publish(new OrderUpdateEvent { Order = order, Remark = "Cancel Rejected: " + @event.RejectionReason });
                    _logger.LogInformation("Order {OrderId} status updated from PendingCancel to {Status}. Reason: {RejectionReason}", @event.OrderId, order.Status, @event.RejectionReason);
                }
                else
                {
                    _logger.LogInformation("Order {OrderId} is not in PendingCancel status.", @event.OrderId);
                }
            }
            else
            {
                _logger.LogWarning("Order {OrderId} not found.", @event.OrderId);
            }
            return Task.CompletedTask;
        }
    }
}
