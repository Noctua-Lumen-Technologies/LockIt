using NUnit.Framework;

namespace NLTechnologies.LockIt.Tests;

[TestFixture]
public class AsyncKeyedLockerOptionsTests
{
    [Test]
    public void Defaults_AreReasonable()
    {
        var options = new AsyncKeyedLockerOptions();

        Assert.Multiple(() =>
        {
            Assert.That(options.LockIdleCleanupInterval, Is.EqualTo(TimeSpan.FromSeconds(60)));
            Assert.That(options.LockIdleCleanupThreshold, Is.EqualTo(TimeSpan.FromSeconds(10)));
            Assert.That(options.LongHeldLockLoggingInterval, Is.EqualTo(TimeSpan.FromSeconds(10)));
            Assert.That(options.LongHeldLockThreshold, Is.EqualTo(TimeSpan.FromSeconds(30)));
            Assert.That(options.DisposeDrainTimeout, Is.Null);
        });
    }

    [Test]
    public void CustomValues_AreApplied()
    {
        var options = new AsyncKeyedLockerOptions
        {
            LockIdleCleanupInterval = TimeSpan.FromMinutes(5),
            LockIdleCleanupThreshold = TimeSpan.FromMinutes(1),
            LongHeldLockLoggingInterval = TimeSpan.FromSeconds(20),
            LongHeldLockThreshold = TimeSpan.FromMinutes(2),
            DisposeDrainTimeout = TimeSpan.FromSeconds(45),
        };

        Assert.Multiple(() =>
        {
            Assert.That(options.LockIdleCleanupInterval, Is.EqualTo(TimeSpan.FromMinutes(5)));
            Assert.That(options.LockIdleCleanupThreshold, Is.EqualTo(TimeSpan.FromMinutes(1)));
            Assert.That(options.LongHeldLockLoggingInterval, Is.EqualTo(TimeSpan.FromSeconds(20)));
            Assert.That(options.LongHeldLockThreshold, Is.EqualTo(TimeSpan.FromMinutes(2)));
            Assert.That(options.DisposeDrainTimeout, Is.EqualTo(TimeSpan.FromSeconds(45)));
        });
    }
}