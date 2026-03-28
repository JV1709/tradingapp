using Model.Domain;
using Model.Request;

namespace MatchingEngine
{
    public class MatchingEngineCommand
    {
        public bool IsCancel { get; set; }
        public Order? Order { get; set; }
        public CancelOrderRequest? CancelRequest { get; set; }

        public static MatchingEngineCommand CreateAddOrder(Order order)
        {
            return new MatchingEngineCommand
            {
                IsCancel = false,
                Order = order
            };
        }

        public static MatchingEngineCommand CreateCancelOrder(CancelOrderRequest cancelRequest)
        {
            return new MatchingEngineCommand
            {
                IsCancel = true,
                CancelRequest = cancelRequest
            };
        }
    }
}
