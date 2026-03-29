using Microsoft.AspNetCore.Mvc;
using OrderGateway;

namespace TradingPlatformAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class OrderController : ControllerBase
    {
        private readonly OrderGatewayService _orderGatewayService;

        public OrderController(OrderGatewayService orderGatewayService)
        {
            _orderGatewayService = orderGatewayService;
        }

        [HttpGet("ws/{username}")]
        public async Task SteamWebSocket(string username, CancellationToken cancellationToken)
        {
            if (HttpContext.WebSockets.IsWebSocketRequest)
            {
                using var webSocket = await HttpContext.WebSockets.AcceptWebSocketAsync();
                await _orderGatewayService.ProcessWebSocketSessionAsync(webSocket, username, cancellationToken);
            }
            else
            {
                HttpContext.Response.StatusCode = 400;
            }
        }
    }
}
