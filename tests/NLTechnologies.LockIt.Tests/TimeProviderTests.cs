using Microsoft.Extensions.Time.Testing;
using NUnit.Framework;

namespace NLTechnologies.LockIt.Tests;

[TestFixture]
public class TimeProviderTests
{
    [Test]
    public async Task FakeTimeProvider_CleanupThreshold_ControlledByAdvance()
    {
        var fakeTime = new FakeTimeProvider(DateTimeOffset.UtcNow);
        var options = TestHelper.FastCleanupOptions(idleThreshold: TimeSpan.FromMinutes(5));
        await using var locker = TestHelper.CreateLocker<string>(options, timeProvider: fakeTime);

        var lease = await locker.AcquireAsync("time1");
        await lease.DisposeAsync();

        // Not enough time passed — cleanup should keep it
        fakeTime.Advance(TimeSpan.FromMinutes(1));
        locker.DebugCleanup();
        Assert.That(locker.DebugHasKey("time1"), Is.True);

        // Advance past threshold
        fakeTime.Advance(TimeSpan.FromMinutes(5));
        locker.DebugCleanup();
        Assert.That(locker.DebugHasKey("time1"), Is.False);
    }

    [Test]
    public async Task FakeTimeProvider_LockRef_UsesInjectedTime()
    {
        var fakeTime = new FakeTimeProvider(DateTimeOffset.UtcNow);
        await using var locker = TestHelper.CreateLocker<string>(timeProvider: fakeTime);

        await using var lease = await locker.AcquireAsync("time2");

        Assert.That(locker.DebugGetRefCount("time2"), Is.EqualTo(1));
    }
}