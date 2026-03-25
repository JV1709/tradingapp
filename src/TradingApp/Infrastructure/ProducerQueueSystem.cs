using System.Collections.Concurrent;

namespace Infrastructure
{
    public interface IProducerQueueSystem<T> where T : class
    {
        event EventHandler<ProducerQueueRegisteredEventArgs<T>>? QueueRegistered;
        event EventHandler<ProducerQueueUnregisteredEventArgs<T>>? QueueUnregistered;

        bool TryRegisterProducer(string producerId, out SPSCQueue<T> queue);
        bool TryGetQueue(string producerId, out SPSCQueue<T> queue);
        bool UnregisterProducer(string producerId);
    }

    public sealed class ProducerQueueRegisteredEventArgs<T> : EventArgs where T : class
    {
        public ProducerQueueRegisteredEventArgs(string producerId, SPSCQueue<T> queue)
        {
            ProducerId = producerId;
            Queue = queue;
        }

        public string ProducerId { get; }
        public SPSCQueue<T> Queue { get; }
    }

    public sealed class ProducerQueueUnregisteredEventArgs<T> : EventArgs where T : class
    {
        public ProducerQueueUnregisteredEventArgs(string producerId, SPSCQueue<T> queue)
        {
            ProducerId = producerId;
            Queue = queue;
        }

        public string ProducerId { get; }
        public SPSCQueue<T> Queue { get; }
    }

    public sealed class ProducerQueueSystem<T> : IProducerQueueSystem<T> where T : class
    {
        private readonly int _queueCapacity;
        private readonly ConcurrentDictionary<string, SPSCQueue<T>> _queues = new();

        public ProducerQueueSystem(int queueCapacity)
        {
            if (queueCapacity <= 1)
            {
                throw new ArgumentOutOfRangeException(nameof(queueCapacity), "Queue capacity must be greater than 1.");
            }

            _queueCapacity = queueCapacity;
        }

        public event EventHandler<ProducerQueueRegisteredEventArgs<T>>? QueueRegistered;
        public event EventHandler<ProducerQueueUnregisteredEventArgs<T>>? QueueUnregistered;

        public bool TryRegisterProducer(string producerId, out SPSCQueue<T> queue)
        {
            if (string.IsNullOrWhiteSpace(producerId))
            {
                throw new ArgumentException("Producer id cannot be null or empty.", nameof(producerId));
            }

            queue = new SPSCQueue<T>(_queueCapacity);
            if (!_queues.TryAdd(producerId, queue))
            {
                queue = default!;
                return false;
            }

            QueueRegistered?.Invoke(this, new ProducerQueueRegisteredEventArgs<T>(producerId, queue));
            return true;
        }

        public bool TryGetQueue(string producerId, out SPSCQueue<T> queue)
        {
            return _queues.TryGetValue(producerId, out queue!);
        }

        public bool UnregisterProducer(string producerId)
        {
            if (!_queues.TryRemove(producerId, out var queue))
            {
                return false;
            }

            QueueUnregistered?.Invoke(this, new ProducerQueueUnregisteredEventArgs<T>(producerId, queue));
            return true;
        }
    }
}
