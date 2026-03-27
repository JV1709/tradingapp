namespace Infrastructure.Queue
{
    public interface IPartitionedSPSCQueueSystem<T> where T : class
    {
        SPSCQueue<T> GetQueue(string partitionKey);
        bool TryGetQueue(string partitionKey, out SPSCQueue<T>? queue);
    }

    public class PartitionedSPSCQueueSystem<T> : IPartitionedSPSCQueueSystem<T> where T : class
    {
        private readonly Dictionary<string, SPSCQueue<T>> _queues;

        public PartitionedSPSCQueueSystem(IEnumerable<string> partitionKeys, int queueCapacity = 1024)
        {
            _queues = partitionKeys.ToDictionary(
                k => k, 
                k => new SPSCQueue<T>(queueCapacity), 
                StringComparer.OrdinalIgnoreCase);
        }

        public SPSCQueue<T> GetQueue(string partitionKey)
        {
            if (_queues.TryGetValue(partitionKey, out var queue))
            {
                return queue;
            }
            throw new ArgumentException($"Queue for partition key '{partitionKey}' not found.");
        }

        public bool TryGetQueue(string partitionKey, out SPSCQueue<T>? queue)
        {
            return _queues.TryGetValue(partitionKey, out queue);
        }
    }
}
