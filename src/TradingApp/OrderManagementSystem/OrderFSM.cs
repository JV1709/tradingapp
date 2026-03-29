using Model.Domain;

namespace OrderManagementSystem
{
    internal class OrderFSM
    {
        public OrderStatus CurrentState { get; private set; }

        public OrderFSM()
        {
            CurrentState = OrderStatus.PendingNew;
        }

        public OrderFSM(OrderStatus initialState)
        {
            CurrentState = initialState;
        }

        public bool ProcessEvent(OrderEvent orderEvent)
        {
            OrderStatus nextState = CurrentState switch
            {
                OrderStatus.PendingNew when orderEvent == OrderEvent.Accepted => OrderStatus.New,
                OrderStatus.PendingNew when orderEvent == OrderEvent.Rejected => OrderStatus.Rejected,
                OrderStatus.PendingNew when orderEvent == OrderEvent.PartialFill => OrderStatus.PartiallyFilled,
                OrderStatus.PendingNew when orderEvent == OrderEvent.Fill => OrderStatus.Filled,
                OrderStatus.New when orderEvent == OrderEvent.PartialFill => OrderStatus.PartiallyFilled,
                OrderStatus.New when orderEvent == OrderEvent.Fill => OrderStatus.Filled,
                OrderStatus.New when orderEvent == OrderEvent.CancelRequest => OrderStatus.PendingCancel,
                OrderStatus.PartiallyFilled when orderEvent == OrderEvent.PartialFill => OrderStatus.PartiallyFilled,
                OrderStatus.PartiallyFilled when orderEvent == OrderEvent.Fill => OrderStatus.Filled,
                OrderStatus.PartiallyFilled when orderEvent == OrderEvent.CancelRequest => OrderStatus.PendingCancel,
                OrderStatus.PendingCancel when orderEvent == OrderEvent.Fill => OrderStatus.Filled,
                OrderStatus.PendingCancel when orderEvent == OrderEvent.Cancelled => OrderStatus.Cancelled,
                _ => CurrentState
            };

            if (nextState != CurrentState)
            {
                CurrentState = nextState;
                return true;
            }

            return false;
        }
    }

    public enum OrderEvent
    {
        Accepted,
        Rejected,
        PartialFill,
        Fill,
        CancelRequest,
        Cancelled
    }
}
