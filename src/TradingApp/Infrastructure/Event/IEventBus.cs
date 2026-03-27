namespace Infrastructure.Event
{
    public interface IEventBus
    {
        void Subscribe<TEvent>(IEventHandler<TEvent> handler) where TEvent : class;
        void Unsubscribe<TEvent>(IEventHandler<TEvent> handler) where TEvent : class;
        void Publish<TEvent>(TEvent @event) where TEvent : class;
    }
}
