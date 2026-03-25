using Microsoft.AspNetCore.Mvc;
using Model.Domain;

namespace TradingPlatformAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class OrderController : ControllerBase
    {
        [HttpGet("stream/{username}")]
        public async Task<IActionResult> StreamOrderUpdates(string username, CancellationToken cancellationToken)
        {
            // Simulate streaming order updates for the given username
            Response.ContentType = "text/event-stream";
            try
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    var orderUpdate = new OrderUpdate
                    {
                        OrderId = Guid.NewGuid().ToString(),
                        Status = OrderStatus.PartiallyFilled,
                        FilledQuantity = new Random().Next(1, 100),
                        FilledPrice = (decimal)(new Random().NextDouble() * 100)
                    };
                    await Response.WriteAsJsonAsync(orderUpdate, cancellationToken);
                    await Response.Body.FlushAsync(cancellationToken);
                    await Task.Delay(2000, cancellationToken); // Simulate delay between order updates
                }
            }
            catch (OperationCanceledException)
            {
                // Client disconnected or request was cancelled
            }
            return new EmptyResult();
        }
    }
}
