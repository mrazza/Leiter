namespace Leiter.Tests.Algorithms.Internal;

using System;
using Leiter.Algorithms.Internal;
using Xunit;

/// <summary>
/// Provides unit tests for <see cref="UniqueIntQueue"/>.
/// </summary>
public class UniqueIntQueueTests
{
    [Fact]
    public void EnqueueDequeue_ShouldPreserveFifoAndDeduplicate()
    {
        var queue = new UniqueIntQueue(4);
        Assert.Equal(4, queue.Capacity);
        Assert.Equal(0, queue.Count);

        Assert.True(queue.Enqueue(2));
        Assert.False(queue.Enqueue(2));
        Assert.True(queue.Enqueue(0));
        Assert.True(queue.Enqueue(3));

        Assert.Equal(3, queue.Count);
        Assert.True(queue.Contains(0));
        Assert.False(queue.Contains(1));
        Assert.True(queue.Contains(2));
        Assert.True(queue.Contains(3));

        Assert.Equal(2, queue.Peek());
        Assert.Equal(2, queue.Dequeue());
        Assert.False(queue.Contains(2));
        Assert.Equal(2, queue.Count);

        // Re-enqueueing after Dequeue succeeds cleanly
        Assert.True(queue.Enqueue(2));
        Assert.True(queue.Contains(2));
        Assert.Equal(0, queue.Dequeue());
        Assert.Equal(3, queue.Dequeue());
        Assert.Equal(2, queue.Dequeue());
        Assert.Equal(0, queue.Count);
    }

    [Fact]
    public void RingBuffer_AcrossWraparoundAndClear_ShouldMaintainIntegrity()
    {
        var queue = new UniqueIntQueue(3);

        for (int cycle = 0; cycle < 5; cycle++)
        {
            Assert.True(queue.Enqueue(0));
            Assert.True(queue.Enqueue(1));
            Assert.Equal(0, queue.Dequeue());

            Assert.True(queue.Enqueue(2));
            Assert.False(queue.Enqueue(1));
            Assert.Equal(1, queue.Dequeue());
            Assert.Equal(2, queue.Dequeue());
            Assert.Equal(0, queue.Count);
        }

        Assert.True(queue.Enqueue(1));
        Assert.True(queue.Enqueue(2));
        queue.Clear();

        Assert.Equal(0, queue.Count);
        Assert.False(queue.Contains(1));
        Assert.False(queue.Contains(2));
        Assert.True(queue.Enqueue(2));
        Assert.Equal(2, queue.Dequeue());
    }

    [Fact]
    public void ValidationAndEmptyQueue_ShouldThrowExpectedExceptions()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new UniqueIntQueue(-1));

        var zeroCapacity = new UniqueIntQueue(0);
        Assert.Equal(0, zeroCapacity.Capacity);
        Assert.Equal(0, zeroCapacity.Count);
        Assert.Throws<InvalidOperationException>(() => zeroCapacity.Peek());
        Assert.Throws<InvalidOperationException>(() => zeroCapacity.Dequeue());

        var queue = new UniqueIntQueue(2);
        Assert.Throws<InvalidOperationException>(() => queue.Peek());
        Assert.Throws<InvalidOperationException>(() => queue.Dequeue());
    }
}
