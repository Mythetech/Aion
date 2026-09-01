namespace Aion.Components.Metrics.Services;

public class BoundedQueue<T>
{
    private readonly Queue<T> _queue = new();

    public int Capacity { get; }
    public int Count => _queue.Count;

    public BoundedQueue(int capacity)
    {
        if (capacity <= 0)
            throw new ArgumentOutOfRangeException(nameof(capacity), "Capacity must be greater than zero.");
        Capacity = capacity;
    }

    public void Enqueue(T item)
    {
        if (_queue.Count >= Capacity)
            _queue.Dequeue();
        _queue.Enqueue(item);
    }

    public void Clear() => _queue.Clear();

    public List<T> ToList() => [.. _queue];
}
