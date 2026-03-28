using Infrastructure.Event;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Model.Domain;
using Model.Event;
using Repository;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace QuoteEngine
{
    public class Worker : BackgroundService, IEventHandler<OrderMatchEvent>, IEventHandler<OrderAcceptedEvent>
    {
        private readonly ILogger<Worker> _logger;
        private readonly IEventBus _eventBus;
        private readonly IQuoteRepository _quoteRepository;
        private readonly IOrderRepository _orderRepository;

        public Worker(
            ILogger<Worker> logger,
            IEventBus eventBus,
            IQuoteRepository quoteRepository,
            IOrderRepository orderRepository)
        {
            _logger = logger;
            _eventBus = eventBus;
            _quoteRepository = quoteRepository;
            _orderRepository = orderRepository;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("QuoteEngine Worker starting...");

            _eventBus.Subscribe<OrderMatchEvent>(this);
            _eventBus.Subscribe<OrderAcceptedEvent>(this);

            try
            {
                await Task.Delay(Timeout.Infinite, stoppingToken);
            }
            catch (TaskCanceledException)
            {
                // Expected on shutdown
            }
            finally
            {
                _eventBus.Unsubscribe<OrderMatchEvent>(this);
                _eventBus.Unsubscribe<OrderAcceptedEvent>(this);
            }
        }

        public Task HandleAsync(OrderMatchEvent @event, CancellationToken cancellationToken = default)
        {
            if (Guid.TryParse(@event.OrderId, out var orderId) && _orderRepository.TryGet(orderId, out var order))
            {
                if (!_quoteRepository.TryGet(order.Symbol, out var quote))
                {
                    quote = new Quote { Symbol = order.Symbol };
                }

                quote.LastDonePrice = @event.Price;
                quote.Timestamp = DateTime.UtcNow;

                _quoteRepository.AddOrUpdate(quote);

                _eventBus.Publish(new NewQuoteEvent { Quote = quote });

                _logger.LogInformation("Quote updated on Match for symbol {Symbol}", order.Symbol);
            }

            return Task.CompletedTask;
        }

        public Task HandleAsync(OrderAcceptedEvent @event, CancellationToken cancellationToken = default)
        {
            if (Guid.TryParse(@event.OrderId, out var orderId) && _orderRepository.TryGet(orderId, out var order))
            {
                if (!_quoteRepository.TryGet(order.Symbol, out var quote))
                {
                    quote = new Quote { Symbol = order.Symbol };
                }

                if (order.Side == Side.Buy)
                {
                    quote.BidPrice = order.Price;
                }
                else if (order.Side == Side.Sell)
                {
                    quote.AskPrice = order.Price;
                }

                quote.Timestamp = DateTime.UtcNow;

                _quoteRepository.AddOrUpdate(quote);

                _eventBus.Publish(new NewQuoteEvent { Quote = quote });

                _logger.LogInformation("Quote updated on Accept for symbol {Symbol}", order.Symbol);
            }

            return Task.CompletedTask;
        }
    }
}
