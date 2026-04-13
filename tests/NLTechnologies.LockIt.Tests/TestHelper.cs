using Microsoft.Extensions.Logging;

namespace NLTechnologies.LockIt.Tests;

internal static class TestHelper
{
    public static AsyncKeyedLocker<TKey> CreateLocker<TKey>(
        AsyncKeyedLockerOptions? options = null,
        TimeProvider? timeProvider = null,
        LockItMetrics? metrics = null) where TKey : notnull
    {
        var logger = new LoggerFactory().CreateLogger<AsyncKeyedLocker<TKey>>();
        return new AsyncKeyedLocker<TKey>(logger, options, timeProvider, metrics);
    }

    public static AsyncKeyedLockerOptions FastCleanupOptions(
        TimeSpan? cleanupInterval = null,
        TimeSpan? idleThreshold = null) => new()
    {
        LockIdleCleanupInterval = cleanupInterval ?? TimeSpan.FromHours(1),
        LockIdleCleanupThreshold = idleThreshold ?? TimeSpan.FromMilliseconds(1),
        LongHeldLockLoggingInterval = TimeSpan.FromHours(1),
        LongHeldLockThreshold = TimeSpan.FromHours(1),
    };
}