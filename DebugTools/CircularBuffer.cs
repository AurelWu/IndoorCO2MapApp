using System.Collections;

namespace IndoorCO2MapAppV2.DebugTools
{
    /// <summary>
    /// Thread-safe fixed-capacity ring buffer. When full, the oldest entry is overwritten.
    /// </summary>
    internal sealed class CircularBuffer<T> : IEnumerable<T>
    {
        private readonly T[] _buffer;
        private readonly int _capacity;
        private int _head;   // next write index
        private int _count;
        private readonly object _lock = new();

        internal CircularBuffer(int capacity)
        {
            _capacity = capacity;
            _buffer = new T[capacity];
        }

        internal void Add(T item)
        {
            lock (_lock)
            {
                _buffer[_head] = item;
                _head = (_head + 1) % _capacity;
                if (_count < _capacity) _count++;
            }
        }

        public IEnumerator<T> GetEnumerator()
        {
            T[] snapshot;
            int start, count;
            lock (_lock)
            {
                snapshot = (T[])_buffer.Clone();
                start = _count < _capacity ? 0 : _head;
                count = _count;
            }
            for (int i = 0; i < count; i++)
                yield return snapshot[(start + i) % _capacity];
        }

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
