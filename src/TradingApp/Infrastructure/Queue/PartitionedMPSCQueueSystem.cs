namespace Infrastructure.Queue
{
    public interface IPartitionedMPSCQueueSystem<T> where T : class
    {
        MPSCQueue<T> GetQueue(string partitionKey);
        bool TryGetQueue(string partitionKey, out MPSCQueue<T>? queue);
    }

    public class PartitionedMPSCQueueSystem<T> : IPartitionedMPSCQueueSystem<T> where T : class
    {
        private readonly Dictionary<string, MPSCQueue<T>> _queues;

        public PartitionedMPSCQueueSystem(IEnumerable<string> partitionKeys, int queueCapacity = 1024)
        {
            _queues = partitionKeys.ToDictionary(
                k => k, 
                k => new MPSCQueue<T>(queueCapacity), 
                StringComparer.OrdinalIgnoreCase);
        }

        public MPSCQueue<T> GetQueue(string partitionKey)
        {
            if (_queues.TryGetValue(partitionKey, out var queue))
            {
                return queue;
            }
            throw new ArgumentException($"Queue for partition key '{partitionKey}' not found.");
        }

        public bool TryGetQueue(string partitionKey, out MPSCQueue<T>? queue)
        {
            return _queues.TryGetValue(partitionKey, out queue);
        }
    }
}
