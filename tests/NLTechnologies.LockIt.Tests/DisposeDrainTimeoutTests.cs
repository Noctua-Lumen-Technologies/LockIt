using NUnit.Framework;

namespace NLTechnologies.LockIt.Tests;

[TestFixture]
public class DisposeDrainTimeoutTests
{
    [Test]
    public async Task DisposeAsync_WithDrainTimeout_ForcesDisposal()
    {
        var options = new AsyncKeyedLockerOptions { DisposeDrainTimeout = TimeSpan.FromMilliseconds(200) };
        var locker = TestHelper.CreateLocker<string>(options);

        // Acquire a lock but never release it
        var lease = await locker.AcquireAsync("stuck");

        // Dispose should force-complete after timeout instead of waiting forever
        var disposeTask = locker.DisposeAsync().AsTask();
        var completed = await Task.WhenAny(disposeTask, Task.Delay(TimeSpan.FromSeconds(5)));

        Assert.That(completed, Is.SameAs(disposeTask), "Dispose should have completed due to drain timeout.");

        // Clean up
        await lease.DisposeAsync();
    }

    [Test]
    public async Task DisposeAsync_WithDrainTimeout_DrainCompletsBeforeTimeout()
    {
        var options = new AsyncKeyedLockerOptions { DisposeDrainTimeout = TimeSpan.FromSeconds(10) };
        var locker = TestHelper.CreateLocker<string>(options);

        var lease = await locker.AcquireAsync("fast");

        var disposeTask = locker.DisposeAsync().AsTask();
        await Task.Delay(50);
        Assert.That(disposeTask.IsCompleted, Is.False);

        await lease.DisposeAsync();
        await disposeTask.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.That(disposeTask.IsCompleted, Is.True);
    }

    [Test]
    public async Task DisposeAsync_NoDrainTimeout_WaitsIndefinitely()
    {
        var options = new AsyncKeyedLockerOptions { DisposeDrainTimeout = null };
        var locker = TestHelper.CreateLocker<string>(options);

        var lease = await locker.AcquireAsync("infinite");

        var disposeTask = locker.DisposeAsync().AsTask();
        await Task.Delay(200);
        Assert.That(disposeTask.IsCompleted, Is.False, "Should wait indefinitely without timeout.");

        await lease.DisposeAsync();
        await disposeTask.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.That(disposeTask.IsCompleted, Is.True);
    }
}