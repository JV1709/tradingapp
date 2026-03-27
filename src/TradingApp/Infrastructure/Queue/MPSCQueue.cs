using System.Runtime.InteropServices;

namespace Infrastructure.Queue
{
    /// <summary>
    /// A bounded, lock-free Multiple-Producer Single-Consumer (MPSC) queue.
    /// Based on Dmitry Vyukov's bounded array queue, optimized for a single consumer.
    /// </summary>
    public class MPSCQueue<T> where T : class
    {
        [StructLayout(LayoutKind.Explicit, Size = 192)]
        private struct PaddedPosition
        {
            [FieldOffset(64)]
            public volatile int Value;
        }

        private struct Cell
        {
            public volatile int Sequence;
            public T Element;
        }

        private readonly Cell[] _buffer;
        private readonly int _mask;

        private PaddedPosition _enqueuePos;
        private PaddedPosition _dequeuePos;

        public MPSCQueue(int capacity)
        {
            int powerOfTwo = 1;
            while (powerOfTwo < capacity)
            {
                powerOfTwo <<= 1;
            }

            _buffer = new Cell[powerOfTwo];
            _mask = powerOfTwo - 1;

            for (int i = 0; i < powerOfTwo; i++)
            {
                _buffer[i].Sequence = i;
            }

            _enqueuePos = new PaddedPosition { Value = 0 };
            _dequeuePos = new PaddedPosition { Value = 0 };
        }

        public bool TryEnqueue(T item)
        {
            Cell[] buffer = _buffer;
            int mask = _mask;
            int pos = _enqueuePos.Value;

            while (true)
            {
                int index = pos & mask;
                int seq = buffer[index].Sequence; // Volatile read
                int diff = seq - pos;

                if (diff == 0)
                {
                    // Slot is ready for our enqueue position; try to claim it
                    if (Interlocked.CompareExchange(ref _enqueuePos.Value, pos + 1, pos) == pos)
                    {
                        buffer[index].Element = item;
                        buffer[index].Sequence = pos + 1; // Volatile write publishes the item to consumer
                        return true;
                    }
                }
                else if (diff < 0)
                {
                    // Sequence is behind position, meaning the queue is full
                    return false;
                }

                // Read fresh position and retry
                pos = _enqueuePos.Value;
            }
        }

        public bool TryDequeue(out T item)
        {
            Cell[] buffer = _buffer;
            int mask = _mask;
            int pos = _dequeuePos.Value; // Single consumer owns this position

            int index = pos & mask;
            int seq = buffer[index].Sequence; // Volatile read
            int diff = seq - (pos + 1);

            if (diff == 0)
            {
                // Sequence is exactly pos + 1, meaning producer has committed the item
                _dequeuePos.Value = pos + 1;
                item = buffer[index].Element;
                buffer[index].Element = default!; // Avoid memory leaks for reference types
                buffer[index].Sequence = pos + mask + 1; // Volatile write publishes empty slot to producers
                return true;
            }

            item = default!;
            return false; // Queue empty
        }
    }
}