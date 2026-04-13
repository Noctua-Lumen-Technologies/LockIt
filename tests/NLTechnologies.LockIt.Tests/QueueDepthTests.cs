using NUnit.Framework;

namespace NLTechnologies.LockIt.Tests;

[TestFixture]
public class QueueDepthTests
{
    [Test]
    public async Task GetQueueDepth_NoKey_ReturnsZero()
    {
        await using var locker = TestHelper.CreateLocker<string>();

        Assert.That(locker.GetQueueDepth("missing"), Is.EqualTo(0));
    }

    [Test]
    public async Task GetQueueDepth_SingleHolder_ReturnsOne()
    {
        await using var locker = TestHelper.CreateLocker<string>();

        await using var lease = await locker.AcquireAsync("depth1");

        Assert.That(locker.GetQueueDepth("depth1"), Is.EqualTo(1));
    }

    [Test]
    public async Task GetQueueDepth_HolderPlusWaiters_ReturnsCorrectCount()
    {
        await using var locker = TestHelper.CreateLocker<string>();
        var lease = await locker.AcquireAsync("depth2");

        var waiter1 = Task.Run(async () => { await using var l = await locker.AcquireAsync("depth2"); });
        var waiter2 = Task.Run(async () => { await using var l = await locker.AcquireAsync("depth2"); });

        await Task.Delay(100); // Let waiters register
        Assert.That(locker.GetQueueDepth("depth2"), Is.EqualTo(3)); // 1 holder + 2 waiters

        await lease.DisposeAsync();
        await Task.WhenAll(waiter1, waiter2);
    }

    [Test]
    public async Task GetQueueDepth_AfterRelease_ReturnsZero()
    {
        await using var locker = TestHelper.CreateLocker<string>();

        var lease = await locker.AcquireAsync("depth3");
        await lease.DisposeAsync();

        Assert.That(locker.GetQueueDepth("depth3"), Is.EqualTo(0));
    }
}