using NUnit.Framework;

namespace NLTechnologies.LockIt.Tests;

[TestFixture]
public class TryAcquireResultTests
{
    [Test]
    public async Task TryAcquireAsync_Available_ReturnsAcquiredTrue()
    {
        await using var locker = TestHelper.CreateLocker<string>();

        await using var result = await locker.TryAcquireAsync("try1", TimeSpan.FromSeconds(5));

        Assert.That(result.Acquired, Is.True);
    }

    [Test]
    public async Task TryAcquireAsync_Timeout_ReturnsAcquiredFalse_DoesNotThrow()
    {
        await using var locker = TestHelper.CreateLocker<string>();
        await using var held = await locker.AcquireAsync("try2");

        await using var result = await locker.TryAcquireAsync("try2", TimeSpan.FromMilliseconds(50));

        Assert.That(result.Acquired, Is.False);
    }

    [Test]
    public async Task TryAcquireAsync_CancellationToken_StillThrows()
    {
        await using var locker = TestHelper.CreateLocker<string>();
        await using var held = await locker.AcquireAsync("try3");

        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(50));

        Assert.ThrowsAsync<OperationCanceledException>(async () =>
            await locker.TryAcquireAsync("try3", TimeSpan.FromSeconds(10), cts.Token));
    }

    [Test]
    public async Task TryAcquireAsync_FailureResult_DisposeIsNoOp()
    {
        await using var locker = TestHelper.CreateLocker<string>();
        await using var held = await locker.AcquireAsync("try4");

        var result = await locker.TryAcquireAsync("try4", TimeSpan.FromMilliseconds(30));
        Assert.That(result.Acquired, Is.False);

        // Should not throw
        await result.DisposeAsync();
        await result.DisposeAsync();
    }

    [Test]
    public async Task TryAcquireAsync_SuccessResult_ReleasesOnDispose()
    {
        await using var locker = TestHelper.CreateLocker<string>();

        var result = await locker.TryAcquireAsync("try5", TimeSpan.FromSeconds(5));
        Assert.That(result.Acquired, Is.True);
        Assert.That(locker.DebugGetRefCount("try5"), Is.EqualTo(1));

        await result.DisposeAsync();
        Assert.That(locker.DebugGetRefCount("try5"), Is.EqualTo(0));
    }

    [Test]
    public async Task TryAcquireAsync_Serializes_SameKey()
    {
        await using var locker = TestHelper.CreateLocker<string>();
        int counter = 0;

        var tasks = Enumerable.Range(0, 30).Select(_ => Task.Run(async () =>
        {
            await using var result = await locker.TryAcquireAsync("serial", TimeSpan.FromSeconds(30));
            if (result.Acquired)
            {
                int v = counter;
                await Task.Yield();
                counter = v + 1;
            }
        })).ToArray();

        await Task.WhenAll(tasks);

        Assert.That(counter, Is.EqualTo(30));
    }
}