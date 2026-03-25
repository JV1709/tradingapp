namespace Model.Domain
{
    public enum GatewayRequestType
    {
        PlaceOrder = 1,
        CancelOrder = 2
    }

    public sealed class GatewayRequest
    {
        public GatewayRequestType Type { get; private init; }
        public PlaceOrderRequest? PlaceOrderRequest { get; private init; }
        public CancelOrderRequest? CancelOrderRequest { get; private init; }

        public static GatewayRequest FromPlaceOrder(PlaceOrderRequest request)
        {
            return new GatewayRequest
            {
                Type = GatewayRequestType.PlaceOrder,
                PlaceOrderRequest = request
            };
        }

        public static GatewayRequest FromCancelOrder(CancelOrderRequest request)
        {
            return new GatewayRequest
            {
                Type = GatewayRequestType.CancelOrder,
                CancelOrderRequest = request
            };
        }
    }


    public sealed class PlaceOrderRequest
    {
        public required string AccountKey { get; set; }
        public required string Symbol { get; set; }
        public long Quantity { get; set; }
        public decimal Price { get; set; }
        // Side: 1 for buy, 2 for sell
        public Side Side { get; set; }
    }

    public sealed class CancelOrderRequest
    {
        public required string OrderId { get; set; }
    }
}
