using System.Runtime.InteropServices;

namespace Infrastructure
{
    /// <summary>
    /// A bounded, lock-free Single-Producer Single-Consumer (SPSC) ring buffer queue.
    /// </summary>
    public class SPSCQueue<T>
    {
        private readonly T[] _buffer;
        private readonly int _mask;

        // Padded to prevent false sharing between head and tail pointers
        [StructLayout(LayoutKind.Explicit, Size = 192)]
        private struct PaddedPosition
        {
            [FieldOffset(64)]
            public volatile int Value;
        }

        private PaddedPosition _head;
        private PaddedPosition _tail;

        public SPSCQueue(int capacity)
        {
            // Ensure capacity is a power of 2 for fast modulo arithmetic via bitwise AND
            int powerOfTwo = 1;
            while (powerOfTwo < capacity)
            {
                powerOfTwo <<= 1;
            }

            _buffer = new T[powerOfTwo];
            _mask = powerOfTwo - 1;

            _head = new PaddedPosition { Value = 0 };
            _tail = new PaddedPosition { Value = 0 };
        }

        /// <summary>
        /// Attempts to enqueue an item. Called only by the single producer thread.
        /// </summary>
        public bool TryEnqueue(T item)
        {
            int currentTail = _tail.Value;
            int nextTail = (currentTail + 1) & _mask;

            // Full check: If next tail touches head, queue is full.
            if (nextTail == _head.Value)
            {
                return false;
            }

            _buffer[currentTail] = item;
            _tail.Value = nextTail; // Volatile write, publishes item to consumer
            return true;
        }

        /// <summary>
        /// Attempts to dequeue an item. Called only by the single consumer thread.
        /// </summary>
        public bool TryDequeue(out T item)
        {
            int currentHead = _head.Value;

            // Empty check: If head equals tail, queue is empty.
            if (currentHead == _tail.Value) // Volatile read
            {
                item = default!;
                return false;
            }

            item = _buffer[currentHead];
            _buffer[currentHead] = default!; // Avoid memory leaks for reference types
            _head.Value = (currentHead + 1) & _mask; // Volatile write, publishes consumed slot to producer
            return true;
        }
    }
}
