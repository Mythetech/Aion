using Aion.Components.Metrics.Services;
using Shouldly;

namespace Aion.Test.Unit.Metrics;

public class BoundedQueueTests
{
    [Fact]
    public void Enqueue_UnderCapacity_AddsItem()
    {
        var queue = new BoundedQueue<int>(5);
        queue.Enqueue(1);
        queue.Enqueue(2);
        queue.Count.ShouldBe(2);
        queue.ToList().ShouldBe([1, 2]);
    }

    [Fact]
    public void Enqueue_AtCapacity_RemovesOldest()
    {
        var queue = new BoundedQueue<int>(3);
        queue.Enqueue(1);
        queue.Enqueue(2);
        queue.Enqueue(3);
        queue.Enqueue(4);
        queue.Count.ShouldBe(3);
        queue.ToList().ShouldBe([2, 3, 4]);
    }

    [Fact]
    public void Enqueue_FarOverCapacity_OnlyKeepsNewest()
    {
        var queue = new BoundedQueue<int>(2);
        for (var i = 1; i <= 10; i++)
            queue.Enqueue(i);
        queue.Count.ShouldBe(2);
        queue.ToList().ShouldBe([9, 10]);
    }

    [Fact]
    public void Clear_RemovesAllItems()
    {
        var queue = new BoundedQueue<int>(5);
        queue.Enqueue(1);
        queue.Enqueue(2);
        queue.Clear();
        queue.Count.ShouldBe(0);
        queue.ToList().ShouldBeEmpty();
    }

    [Fact]
    public void Constructor_InvalidCapacity_Throws()
    {
        Should.Throw<ArgumentOutOfRangeException>(() => new BoundedQueue<int>(0));
        Should.Throw<ArgumentOutOfRangeException>(() => new BoundedQueue<int>(-1));
    }

    [Fact]
    public void ToList_ReturnsItemsInFifoOrder()
    {
        var queue = new BoundedQueue<string>(5);
        queue.Enqueue("a");
        queue.Enqueue("b");
        queue.Enqueue("c");
        queue.ToList().ShouldBe(["a", "b", "c"]);
    }
}
