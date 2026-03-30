using Microsoft.AspNetCore.Mvc;
using Infrastructure.Event;
using Model.Domain;
using Model.Event;
using System.Text.Json;
using System.Threading.Channels;
using Repository;

namespace TradingPlatformAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class QuotesController : ControllerBase
    {
        private readonly IQuoteRepository _quoteRepository;
        private readonly IEventBus _eventBus;

        public QuotesController(IQuoteRepository quoteRepository, IEventBus eventBus)
        {
            _quoteRepository = quoteRepository;
            _eventBus = eventBus;
        }

        private class ChannelEventHandler : IEventHandler<NewQuoteEvent>
        {
            private readonly ChannelWriter<NewQuoteEvent> _writer;

            public ChannelEventHandler(ChannelWriter<NewQuoteEvent> writer)
            {
                _writer = writer;
            }

            public async Task HandleAsync(NewQuoteEvent @event, CancellationToken cancellationToken = default)
            {
                await _writer.WriteAsync(@event, cancellationToken);
            }
        }

        [HttpGet("stream/{symbol}")]
        public async Task<IActionResult> StreamQuotes(string symbol, CancellationToken cancellationToken)
        {
            Response.ContentType = "text/event-stream";
            Response.Headers.CacheControl = "no-cache";
            Response.Headers.Connection = "keep-alive";

            _quoteRepository.TryGet(symbol, out var initialQuote);
            if (initialQuote == null)
            {
                initialQuote = new Quote
                {
                    Symbol = symbol,
                    Timestamp = DateTime.UtcNow
                };
            }

            var initialJson = JsonSerializer.Serialize(initialQuote);
            await Response.WriteAsync($"data: {initialJson}\n\n", cancellationToken);
            await Response.Body.FlushAsync(cancellationToken);

            var channel = Channel.CreateUnbounded<NewQuoteEvent>();
            var handler = new ChannelEventHandler(channel.Writer);

            _eventBus.Subscribe(handler);

            try
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    if (!await channel.Reader.WaitToReadAsync(cancellationToken))
                    {
                        break;
                    }

                    while (channel.Reader.TryRead(out var newQuoteEvent))
                    {
                        if (!string.Equals(newQuoteEvent.Quote.Symbol, symbol, StringComparison.OrdinalIgnoreCase))
                        {
                            continue;
                        }

                        var quote = newQuoteEvent.Quote;
                        var json = JsonSerializer.Serialize(quote);
                        await Response.WriteAsync($"data: {json}\n\n", cancellationToken);
                        await Response.Body.FlushAsync(cancellationToken);
                        
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // Client disconnected or request was cancelled
            }
            finally
            {
                _eventBus.Unsubscribe(handler);
            }

            return new EmptyResult();
        }
    }
}
