namespace Leiter.Algorithms.Internal;

using System;

/// <summary>
/// Fixed-capacity FIFO ring-buffer queue for bounded non-negative integers with O(1) membership deduplication.
/// </summary>
internal sealed class UniqueIntQueue
{
    private readonly int[] items;
    private readonly bool[] contains;
    private int head;
    private int tail;

    public UniqueIntQueue(int capacity)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(capacity);

        items = new int[capacity];
        contains = new bool[capacity];
    }

    public int Count { get; private set; }

    public int Capacity => items.Length;

    public bool Contains(int value) => contains[value];

    public void Clear()
    {
        Array.Clear(contains);
        head = 0;
        tail = 0;
        Count = 0;
    }

    public int Peek()
    {
        if (Count == 0)
            throw new InvalidOperationException("Queue is empty.");

        return items[head];
    }

    public bool Enqueue(int value)
    {
        if (contains[value])
            return false;

        contains[value] = true;
        items[tail] = value;
        tail++;
        if (tail == items.Length)
            tail = 0;
        Count++;
        return true;
    }

    public int Dequeue()
    {
        if (Count == 0)
            throw new InvalidOperationException("Queue is empty.");

        int value = items[head];
        contains[value] = false;
        head++;
        if (head == items.Length)
            head = 0;
        Count--;
        return value;
    }
}
