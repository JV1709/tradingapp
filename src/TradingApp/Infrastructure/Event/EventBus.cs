using System.Collections.Concurrent;

namespace Infrastructure.Event
{
    public sealed class EventBus : IEventBus
    {
        private readonly ConcurrentDictionary<Type, object> _handlers = new();

        public void Subscribe<TEvent>(IEventHandler<TEvent> handler) where TEvent : class
        {
            var handlersForType = (ConcurrentDictionary<IEventHandler<TEvent>, byte>)_handlers.GetOrAdd(
                typeof(TEvent), 
                _ => new ConcurrentDictionary<IEventHandler<TEvent>, byte>());

            handlersForType.TryAdd(handler, 1);
        }

        public void Unsubscribe<TEvent>(IEventHandler<TEvent> handler) where TEvent : class
        {
            if (_handlers.TryGetValue(typeof(TEvent), out var handlersObj))
            {
                var handlersForType = (ConcurrentDictionary<IEventHandler<TEvent>, byte>)handlersObj;
                handlersForType.TryRemove(handler, out _);
            }
        }

        public void Publish<TEvent>(TEvent @event) where TEvent : class
        {
            if (_handlers.TryGetValue(typeof(TEvent), out var handlersObj))
            {
                var handlersForType = (ConcurrentDictionary<IEventHandler<TEvent>, byte>)handlersObj;

                foreach (var handler in handlersForType.Keys)
                {
                    Task.Run(() => handler.HandleAsync(@event));
                }
            }
        }
    }
}
