using System.Collections.Concurrent;

namespace Aspire.Hosting.Minecraft.Worker.Services;

/// <summary>
/// Time-series ring buffer for storing per-resource health snapshots.
/// Used by the Redstone Dashboard to render health bars over time.
/// </summary>
internal sealed class HealthHistoryTracker
{
    private readonly ConcurrentDictionary<string, CircularBuffer> _buffers = new();
    private readonly int _capacity;

    public HealthHistoryTracker(int capacity = 10)
    {
        _capacity = capacity;
    }

    /// <summary>
    /// Records a health snapshot for the named resource.
    /// </summary>
    public void Record(string resourceName, ResourceStatus status)
    {
        var buffer = _buffers.GetOrAdd(resourceName, _ => new CircularBuffer(_capacity));
        buffer.Add(status);
    }

    /// <summary>
    /// Returns the last N snapshots for the named resource in chronological order (oldest first).
    /// </summary>
    public IReadOnlyList<ResourceStatus> GetHistory(string resourceName)
    {
        if (_buffers.TryGetValue(resourceName, out var buffer))
        {
            return buffer.ToList();
        }

        return Array.Empty<ResourceStatus>();
    }

    /// <summary>
    /// Returns all tracked resource names.
    /// </summary>
    public IReadOnlyCollection<string> GetAllResources()
    {
        return _buffers.Keys.ToArray();
    }

    /// <summary>
    /// Fixed-size circular buffer that stores ResourceStatus entries.
    /// </summary>
    private sealed class CircularBuffer
    {
        private readonly ResourceStatus[] _items;
        private readonly int _capacity;
        private int _head;
        private int _count;

        public CircularBuffer(int capacity)
        {
            _capacity = capacity;
            _items = new ResourceStatus[capacity];
        }

        public void Add(ResourceStatus status)
        {
            _items[_head] = status;
            _head = (_head + 1) % _capacity;
            if (_count < _capacity)
            {
                _count++;
            }
        }

        public IReadOnlyList<ResourceStatus> ToList()
        {
            var result = new ResourceStatus[_count];
            if (_count < _capacity)
            {
                // Buffer not full: items are at indices 0.._count-1
                Array.Copy(_items, 0, result, 0, _count);
            }
            else
            {
                // Buffer full: oldest item is at _head, wrap around
                var tailLength = _capacity - _head;
                Array.Copy(_items, _head, result, 0, tailLength);
                Array.Copy(_items, 0, result, tailLength, _head);
            }

            return result;
        }
    }
}
