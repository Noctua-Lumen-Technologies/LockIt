using NUnit.Framework;

namespace NLTechnologies.LockIt.Tests;

[TestFixture]
public class AsyncKeyedLockerTimeoutCancellationTests
{
    [Test]
    public async Task AcquireAsync_WithTimeout_ThrowsOnExpiry()
    {
        await using var locker = TestHelper.CreateLocker<string>();

        // Hold the lock so the second acquire times out
        await using var held = await locker.AcquireAsync("t");

        Assert.ThrowsAsync<OperationCanceledException>(async () =>
            await locker.AcquireAsync("t", timeout: TimeSpan.FromMilliseconds(50)));
    }

    [Test]
    public async Task AcquireAsync_WithTimeout_RefCountRestoredOnTimeout()
    {
        await using var locker = TestHelper.CreateLocker<string>();
        await using var held = await locker.AcquireAsync("trc");

        try
        {
            await locker.AcquireAsync("trc", timeout: TimeSpan.FromMilliseconds(50));
        }
        catch (OperationCanceledException) { }

        // Only the held lease should remain
        Assert.That(locker.DebugGetRefCount("trc"), Is.EqualTo(1));
    }

    [Test]
    public async Task AcquireAsync_WithCancellationToken_ThrowsOnCancel()
    {
        await using var locker = TestHelper.CreateLocker<string>();
        await using var held = await locker.AcquireAsync("ct");

        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(50));

        Assert.ThrowsAsync<OperationCanceledException>(async () =>
            await locker.AcquireAsync("ct", cancellationToken: cts.Token));
    }

    [Test]
    public async Task AcquireAsync_PreCancelledToken_ThrowsImmediately()
    {
        await using var locker = TestHelper.CreateLocker<string>();
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var ex = Assert.ThrowsAsync(
            Is.InstanceOf<OperationCanceledException>(),
            async () => await locker.AcquireAsync("pre", cancellationToken: cts.Token));

        Assert.That(ex, Is.Not.Null);
    }

    [Test]
    public async Task AcquireAsync_TimeoutAndCancellationToken_TimeoutExpires()
    {
        await using var locker = TestHelper.CreateLocker<string>();
        await using var held = await locker.AcquireAsync("combo");

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10)); // Long token

        Assert.ThrowsAsync<OperationCanceledException>(async () =>
            await locker.AcquireAsync("combo", timeout: TimeSpan.FromMilliseconds(50), cancellationToken: cts.Token));
    }

    [Test]
    public async Task AcquireAsync_TimeoutAndCancellationToken_TokenCancels()
    {
        await using var locker = TestHelper.CreateLocker<string>();
        await using var held = await locker.AcquireAsync("combo2");

        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(50));

        Assert.ThrowsAsync<OperationCanceledException>(async () =>
            await locker.AcquireAsync("combo2", timeout: TimeSpan.FromSeconds(10), cancellationToken: cts.Token));
    }

    [Test]
    public async Task AcquireAsync_NoTimeout_WaitsIndefinitelyUntilReleased()
    {
        await using var locker = TestHelper.CreateLocker<string>();
        var acquired = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        var lease1 = await locker.AcquireAsync("wait");

        var waiter = Task.Run(async () =>
        {
            await using var lease2 = await locker.AcquireAsync("wait");
            acquired.SetResult();
        });

        // Ensure waiter is blocked
        await Task.Delay(100);
        Assert.That(acquired.Task.IsCompleted, Is.False);

        // Release -> waiter should proceed
        await lease1.DisposeAsync();
        await acquired.Task.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.That(acquired.Task.IsCompleted, Is.True);
    }
}