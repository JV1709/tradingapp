using Microsoft.AspNetCore.Mvc;
using Model.Domain;

namespace TradingPlatformAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class QuoteController : ControllerBase
    {
        [HttpGet("stream/{symbol}")]
        public async Task<IActionResult> StreamQuotes(string symbol, CancellationToken cancellationToken)
        {
            // Simulate streaming quotes for the given symbol
            Response.ContentType = "text/event-stream";
            try
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    var quote = new Quote
                    {
                        Symbol = symbol,
                        BidPrice = 100 + new Random().Next(-5, 5), // Simulated bid price
                        AskPrice = 100 + new Random().Next(-5, 5) + 0.5m, // Simulated ask price
                        Timestamp = DateTime.UtcNow
                    };
                    await Response.WriteAsJsonAsync(quote, cancellationToken);
                    await Response.Body.FlushAsync(cancellationToken);
                    await Task.Delay(1000, cancellationToken); // Simulate delay between quotes
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
