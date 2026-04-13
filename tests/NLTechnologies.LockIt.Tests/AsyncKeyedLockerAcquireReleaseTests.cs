using NUnit.Framework;

namespace NLTechnologies.LockIt.Tests;

[TestFixture]
public class AsyncKeyedLockerAcquireReleaseTests
{
    [Test]
    public async Task AcquireAsync_SingleKey_ReturnsNonNullLease()
    {
        await using var locker = TestHelper.CreateLocker<string>();

        await using var lease = await locker.AcquireAsync("key1");

        Assert.That(lease, Is.Not.Null);
    }

    private static readonly int[] expected = [1, 2, 3];

    [Test]
    public async Task AcquireAsync_SameKey_SerializesAccess()
    {
        await using var locker = TestHelper.CreateLocker<string>();
        var order = new List<int>();
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        var task1 = Task.Run(async () =>
        {
            await using var lease = await locker.AcquireAsync("key");
            order.Add(1);
            gate.SetResult();
            await Task.Delay(100);
            order.Add(2);
        });

        await gate.Task;

        var task2 = Task.Run(async () =>
        {
            await using var lease = await locker.AcquireAsync("key");
            order.Add(3);
        });

        await Task.WhenAll(task1, task2);

        Assert.That(order, Is.EqualTo(expected));
    }

    [Test]
    public async Task AcquireAsync_DifferentKeys_RunInParallel()
    {
        await using var locker = TestHelper.CreateLocker<string>();
        var bothRunning = new CountdownEvent(2);
        var proceed = new ManualResetEventSlim(false);

        var task1 = Task.Run(async () =>
        {
            await using var lease = await locker.AcquireAsync("A");
            bothRunning.Signal();
            proceed.Wait();
        });

        var task2 = Task.Run(async () =>
        {
            await using var lease = await locker.AcquireAsync("B");
            bothRunning.Signal();
            proceed.Wait();
        });

        bool bothReached = bothRunning.Wait(TimeSpan.FromSeconds(5));
        proceed.Set();
        await Task.WhenAll(task1, task2);

        Assert.That(bothReached, Is.True, "Both keys should run concurrently.");
    }

    [Test]
    public async Task AcquireAsync_ReleaseThenReacquire_SameKey_Succeeds()
    {
        await using var locker = TestHelper.CreateLocker<string>();

        // First acquire + release
        var lease1 = await locker.AcquireAsync("reuse");
        await lease1.DisposeAsync();

        // Second acquire — reuses existing LockRef
        await using var lease2 = await locker.AcquireAsync("reuse");

        Assert.That(lease2, Is.Not.Null);
    }

    [Test]
    public async Task AcquireAsync_NullKey_ThrowsArgumentNullException()
    {
        await using var locker = TestHelper.CreateLocker<string>();

        Assert.ThrowsAsync<ArgumentNullException>(async () => await locker.AcquireAsync(null!));
    }

    [Test]
    public async Task AcquireAsync_RefCount_IncrementsAndDecrements()
    {
        await using var locker = TestHelper.CreateLocker<string>();

        var lease = await locker.AcquireAsync("rc");
        Assert.That(locker.DebugGetRefCount("rc"), Is.EqualTo(1));

        await lease.DisposeAsync();
        Assert.That(locker.DebugGetRefCount("rc"), Is.EqualTo(0));
    }

    [Test]
    public async Task AcquireAsync_MultipleWaiters_AllComplete()
    {
        await using var locker = TestHelper.CreateLocker<int>();
        int counter = 0;

        var tasks = Enumerable.Range(0, 20).Select(_ => Task.Run(async () =>
        {
            await using var lease = await locker.AcquireAsync(42);
            Interlocked.Increment(ref counter);
            await Task.Delay(5);
        })).ToArray();

        await Task.WhenAll(tasks);

        Assert.That(counter, Is.EqualTo(20));
    }

    [Test]
    public async Task Release_DoubleDispose_IsIdempotent()
    {
        await using var locker = TestHelper.CreateLocker<string>();

        var lease = await locker.AcquireAsync("dd");
        await lease.DisposeAsync();
        await lease.DisposeAsync(); // Should not throw or double-release

        Assert.That(locker.DebugGetRefCount("dd"), Is.EqualTo(0));
    }
}