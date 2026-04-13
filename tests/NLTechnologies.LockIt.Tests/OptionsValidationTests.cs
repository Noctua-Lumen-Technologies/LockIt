using NUnit.Framework;

namespace NLTechnologies.LockIt.Tests;

[TestFixture]
public class OptionsValidationTests
{
    [Test]
    public void ZeroCleanupInterval_ThrowsArgumentOutOfRangeException()
    {
        var options = new AsyncKeyedLockerOptions { LockIdleCleanupInterval = TimeSpan.Zero };

        Assert.Throws<ArgumentOutOfRangeException>(() => TestHelper.CreateLocker<string>(options));
    }

    [Test]
    public void NegativeCleanupThreshold_ThrowsArgumentOutOfRangeException()
    {
        var options = new AsyncKeyedLockerOptions { LockIdleCleanupThreshold = TimeSpan.FromSeconds(-1) };

        Assert.Throws<ArgumentOutOfRangeException>(() => TestHelper.CreateLocker<string>(options));
    }

    [Test]
    public void ZeroLongHeldInterval_ThrowsArgumentOutOfRangeException()
    {
        var options = new AsyncKeyedLockerOptions { LongHeldLockLoggingInterval = TimeSpan.Zero };

        Assert.Throws<ArgumentOutOfRangeException>(() => TestHelper.CreateLocker<string>(options));
    }

    [Test]
    public void NegativeLongHeldThreshold_ThrowsArgumentOutOfRangeException()
    {
        var options = new AsyncKeyedLockerOptions { LongHeldLockThreshold = TimeSpan.FromSeconds(-5) };

        Assert.Throws<ArgumentOutOfRangeException>(() => TestHelper.CreateLocker<string>(options));
    }

    [Test]
    public void ZeroDisposeDrainTimeout_ThrowsArgumentOutOfRangeException()
    {
        var options = new AsyncKeyedLockerOptions { DisposeDrainTimeout = TimeSpan.Zero };

        Assert.Throws<ArgumentOutOfRangeException>(() => TestHelper.CreateLocker<string>(options));
    }

    [Test]
    public void NullDisposeDrainTimeout_DoesNotThrow()
    {
        var options = new AsyncKeyedLockerOptions { DisposeDrainTimeout = null };

        Assert.DoesNotThrow(() =>
        {
            using var locker = TestHelper.CreateLocker<string>(options);
        });
    }

    [Test]
    public void ValidOptions_DoNotThrow()
    {
        var options = new AsyncKeyedLockerOptions
        {
            LockIdleCleanupInterval = TimeSpan.FromSeconds(1),
            LockIdleCleanupThreshold = TimeSpan.FromSeconds(1),
            LongHeldLockLoggingInterval = TimeSpan.FromSeconds(1),
            LongHeldLockThreshold = TimeSpan.FromSeconds(1),
            DisposeDrainTimeout = TimeSpan.FromSeconds(30),
        };

        Assert.DoesNotThrow(() =>
        {
            using var locker = TestHelper.CreateLocker<string>(options);
        });
    }
}