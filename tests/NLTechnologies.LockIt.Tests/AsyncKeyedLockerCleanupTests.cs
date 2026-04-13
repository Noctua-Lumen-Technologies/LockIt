using NUnit.Framework;

namespace NLTechnologies.LockIt.Tests;

[TestFixture]
public class AsyncKeyedLockerCleanupTests
{
    [Test]
    public async Task Cleanup_RemovesIdleLock_AfterThreshold()
    {
        var options = TestHelper.FastCleanupOptions(idleThreshold: TimeSpan.FromMilliseconds(1));
        await using var locker = TestHelper.CreateLocker<string>(options);

        var lease = await locker.AcquireAsync("idle");
        await lease.DisposeAsync();

        // Wait for idle threshold to pass
        await Task.Delay(50);
        locker.DebugCleanup();

        Assert.That(locker.DebugHasKey("idle"), Is.False);
    }

    [Test]
    public async Task Cleanup_DoesNotRemove_HeldLock()
    {
        var options = TestHelper.FastCleanupOptions(idleThreshold: TimeSpan.FromMilliseconds(1));
        await using var locker = TestHelper.CreateLocker<string>(options);

        await using var lease = await locker.AcquireAsync("held");
        await Task.Delay(50);
        locker.DebugCleanup();

        Assert.That(locker.DebugHasKey("held"), Is.True);
    }

    [Test]
    public async Task Cleanup_DoesNotRemove_LockWithWaiters()
    {
        var options = TestHelper.FastCleanupOptions(idleThreshold: TimeSpan.FromMilliseconds(1));
        await using var locker = TestHelper.CreateLocker<string>(options);

        var lease = await locker.AcquireAsync("waiters");

        // Start a waiter
        var waiterTask = Task.Run(async () =>
        {
            await using var l = await locker.AcquireAsync("waiters");
        });

        await Task.Delay(50);
        locker.DebugCleanup();

        // RefCount > 0 — should not be removed
        Assert.That(locker.DebugHasKey("waiters"), Is.True);

        await lease.DisposeAsync();
        await waiterTask;
    }

    [Test]
    public async Task Cleanup_DoesNotRemove_RecentlyReleasedLock()
    {
        var options = TestHelper.FastCleanupOptions(idleThreshold: TimeSpan.FromMinutes(5));
        await using var locker = TestHelper.CreateLocker<string>(options);

        var lease = await locker.AcquireAsync("recent");
        await lease.DisposeAsync();

        locker.DebugCleanup();

        // Threshold is 5 minutes — should still be present
        Assert.That(locker.DebugHasKey("recent"), Is.True);
    }

    [Test]
    public async Task Cleanup_AfterRemoval_NewAcquire_CreatesNewLockRef()
    {
        var options = TestHelper.FastCleanupOptions(idleThreshold: TimeSpan.FromMilliseconds(1));
        await using var locker = TestHelper.CreateLocker<string>(options);

        var lease = await locker.AcquireAsync("recreate");
        await lease.DisposeAsync();

        await Task.Delay(50);
        locker.DebugCleanup();
        Assert.That(locker.DebugHasKey("recreate"), Is.False);

        // Reacquire — new LockRef should be created
        await using var lease2 = await locker.AcquireAsync("recreate");
        Assert.That(locker.DebugHasKey("recreate"), Is.True);
    }

    [Test]
    public async Task KeyLifecycle_FullCycle_AcquireReleaseIdleCleanup()
    {
        var options = TestHelper.FastCleanupOptions(idleThreshold: TimeSpan.FromMilliseconds(1));
        await using var locker = TestHelper.CreateLocker<string>(options);

        // 1. Acquire
        var lease = await locker.AcquireAsync("lifecycle");
        Assert.That(locker.DebugGetRefCount("lifecycle"), Is.EqualTo(1));

        // 2. Release
        await lease.DisposeAsync();
        Assert.That(locker.DebugGetRefCount("lifecycle"), Is.EqualTo(0));
        Assert.That(locker.DebugHasKey("lifecycle"), Is.True); // Still in dictionary

        // 3. Re-acquire (reuses same LockRef)
        var lease2 = await locker.AcquireAsync("lifecycle");
        Assert.That(locker.DebugGetRefCount("lifecycle"), Is.EqualTo(1));
        await lease2.DisposeAsync();

        // 4. Idle → cleanup
        await Task.Delay(50);
        locker.DebugCleanup();
        Assert.That(locker.DebugHasKey("lifecycle"), Is.False); // Gone
    }
}