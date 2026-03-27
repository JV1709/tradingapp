using Microsoft.AspNetCore.Mvc;
using Model.Event;
using Infrastructure.Event;
using System.Threading.Channels;
using Repository;

namespace TradingPlatformAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class OrderController : ControllerBase
    {
        private readonly IOrderRepository _orderRepository;
        private readonly IEventBus _eventBus;

        public OrderController(IOrderRepository orderRepository, IEventBus eventBus)
        {
            _orderRepository = orderRepository;
            _eventBus = eventBus;
        }

        private class ChannelEventHandler : IEventHandler<OrderUpdateEvent>
        {
            private readonly ChannelWriter<OrderUpdateEvent> _writer;

            public ChannelEventHandler(ChannelWriter<OrderUpdateEvent> writer)
            {
                _writer = writer;
            }

            public async Task HandleAsync(OrderUpdateEvent @event, CancellationToken cancellationToken = default)
            {
                await _writer.WriteAsync(@event, cancellationToken);
            }
        }

        [HttpGet("stream/{username}")]
        public async Task<IActionResult> StreamOrderUpdates(string username, CancellationToken cancellationToken)
        {
            Response.ContentType = "text/event-stream";

            var initialOrders = _orderRepository.GetByAccountKey(username);
            foreach (var order in initialOrders)
            {
                await Response.WriteAsJsonAsync(initialOrders, cancellationToken);
                await Response.Body.WriteAsync(new byte[] { (byte)'\n' }, cancellationToken);
            }
            await Response.Body.FlushAsync(cancellationToken);

            var channel = Channel.CreateUnbounded<OrderUpdateEvent>();
            var handler = new ChannelEventHandler(channel.Writer);
            
            _eventBus.Subscribe(handler);

            try
            {
                await foreach (var orderUpdate in channel.Reader.ReadAllAsync(cancellationToken))
                {
                    if (orderUpdate.Order.AccountKey != username)
                        continue;

                    await Response.WriteAsJsonAsync(orderUpdate, cancellationToken);
                    await Response.Body.WriteAsync(new byte[] { (byte)'\n' }, cancellationToken);
                    await Response.Body.FlushAsync(cancellationToken);
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
