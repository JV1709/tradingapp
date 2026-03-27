using Microsoft.AspNetCore.Mvc;
using Infrastructure.Event;
using Model.Domain;
using Model.Event;
using System.Threading.Channels;
using Repository;

namespace TradingPlatformAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class QuoteController : ControllerBase
    {
        private readonly IQuoteRepository _quoteRepository;
        private readonly IEventBus _eventBus;

        public QuoteController(IQuoteRepository quoteRepository, IEventBus eventBus)
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

            _quoteRepository.TryGet(symbol, out var initialQuote);
            if (initialQuote == null)
            {
                initialQuote = new Quote
                {
                    Symbol = symbol,
                    Timestamp = DateTime.UtcNow
                };
            }

            await Response.WriteAsJsonAsync(initialQuote, cancellationToken);
            await Response.Body.WriteAsync(new byte[] { (byte)'\n' }, cancellationToken);
            await Response.Body.FlushAsync(cancellationToken);

            var channel = Channel.CreateUnbounded<NewQuoteEvent>();
            var handler = new ChannelEventHandler(channel.Writer);

            _eventBus.Subscribe(handler);

            try
            {
                await foreach (var newQuoteEvent in channel.Reader.ReadAllAsync(cancellationToken))
                {
                    if (string.Equals(newQuoteEvent.Quote.Symbol, symbol, StringComparison.OrdinalIgnoreCase))
                    {
                        var quote = newQuoteEvent.Quote;
                        await Response.WriteAsJsonAsync(quote, cancellationToken);
                        await Response.Body.WriteAsync(new byte[] { (byte)'\n' }, cancellationToken);
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
