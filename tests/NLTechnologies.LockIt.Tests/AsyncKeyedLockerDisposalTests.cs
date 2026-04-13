using NUnit.Framework;

namespace NLTechnologies.LockIt.Tests;

[TestFixture]
public class AsyncKeyedLockerDisposalTests
{
    [Test]
    public async Task DisposeAsync_NoActiveLocks_CompletesImmediately()
    {
        var locker = TestHelper.CreateLocker<string>();

        var disposeTask = locker.DisposeAsync();

        Assert.That(disposeTask.IsCompleted || await Task.Run(async () =>
        {
            await disposeTask;
            return true;
        }).WaitAsync(TimeSpan.FromSeconds(2)), Is.True);
    }

    [Test]
    public async Task DisposeAsync_WithActiveLock_WaitsForDrain()
    {
        var locker = TestHelper.CreateLocker<string>();
        var lease = await locker.AcquireAsync("drain");

        var disposeTask = locker.DisposeAsync().AsTask();

        // Dispose should be waiting
        await Task.Delay(100);
        Assert.That(disposeTask.IsCompleted, Is.False);

        // Release the lock — dispose should complete
        await lease.DisposeAsync();
        await disposeTask.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.That(disposeTask.IsCompleted, Is.True);
    }

    [Test]
    public async Task DisposeAsync_WithMultipleActiveLocks_WaitsForAll()
    {
        var locker = TestHelper.CreateLocker<string>();
        var lease1 = await locker.AcquireAsync("d1");
        var lease2 = await locker.AcquireAsync("d2");

        var disposeTask = locker.DisposeAsync().AsTask();

        await lease1.DisposeAsync();
        await Task.Delay(50);
        Assert.That(disposeTask.IsCompleted, Is.False, "Should still wait for d2");

        await lease2.DisposeAsync();
        await disposeTask.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.That(disposeTask.IsCompleted, Is.True);
    }

    [Test]
    public async Task DisposeAsync_RejectsNewAcquireAsync()
    {
        var locker = TestHelper.CreateLocker<string>();
        await locker.DisposeAsync();

        Assert.ThrowsAsync<ObjectDisposedException>(async () =>
            await locker.AcquireAsync("rejected"));
    }

    [Test]
    public async Task DisposeAsync_Idempotent_CallingMultipleTimes_DoesNotThrow()
    {
        var locker = TestHelper.CreateLocker<string>();

        await locker.DisposeAsync();
        await locker.DisposeAsync();
        await locker.DisposeAsync();

        Assert.Pass("Multiple DisposeAsync calls did not throw.");
    }

    [Test]
    public void Dispose_Synchronous_CompletesWithoutDeadlock()
    {
        var locker = TestHelper.CreateLocker<string>();

        Assert.DoesNotThrow(() => locker.Dispose());
    }

    [Test]
    public void Dispose_Synchronous_Idempotent()
    {
        var locker = TestHelper.CreateLocker<string>();

        locker.Dispose();
        locker.Dispose();
        locker.Dispose();

        Assert.Pass("Multiple Dispose calls did not throw.");
    }

    [Test]
    public async Task Dispose_SyncWhileLockHeld_WaitsForRelease()
    {
        var locker = TestHelper.CreateLocker<string>();
        var lease = await locker.AcquireAsync("syncwait");

        var disposeThread = Task.Run(() => locker.Dispose());

        await Task.Delay(100);
        Assert.That(disposeThread.IsCompleted, Is.False);

        await lease.DisposeAsync();
        await disposeThread.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.That(disposeThread.IsCompleted, Is.True);
    }

    [Test]
    public async Task DisposeAsync_ConcurrentDisposeAndRelease_NoCrash()
    {
        var locker = TestHelper.CreateLocker<string>();
        var leases = new List<IAsyncDisposable>();

        for (int i = 0; i < 50; i++)
        {
            leases.Add(await locker.AcquireAsync($"k{i}"));
        }

        // Start dispose and release all concurrently
        var disposeTask = locker.DisposeAsync().AsTask();
        var releaseTasks = leases.Select(l => l.DisposeAsync().AsTask()).ToArray();

        await Task.WhenAll(releaseTasks.Append(disposeTask));

        Assert.Pass("Concurrent dispose and release completed without crash.");
    }
}