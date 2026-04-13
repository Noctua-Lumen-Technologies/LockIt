namespace NLTechnologies.LockIt;

/// <summary>
/// Configuration options for <see cref="AsyncKeyedLocker{TKey}"/>.
/// All <see cref="TimeSpan"/> values must be positive. Validation occurs when the locker is constructed.
/// </summary>
public sealed class AsyncKeyedLockerOptions
{
    /// <summary>
    /// How often the locker scans for idle locks to remove. Default 60s.
    /// </summary>
    public TimeSpan LockIdleCleanupInterval { get; set; } = TimeSpan.FromSeconds(60);

    /// <summary>
    /// How long a lock must be idle (no acquisitions) before it becomes eligible for removal. Default 10s.
    /// </summary>
    public TimeSpan LockIdleCleanupThreshold { get; set; } = TimeSpan.FromSeconds(10);

    /// <summary>
    /// Interval at which long-held locks are checked and possibly logged. Default 10s.
    /// </summary>
    public TimeSpan LongHeldLockLoggingInterval { get; set; } = TimeSpan.FromSeconds(10);

    /// <summary>
    /// If a lock is held longer than this threshold a warning is logged. Default 30s.
    /// </summary>
    public TimeSpan LongHeldLockThreshold { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Maximum time <see cref="AsyncKeyedLocker{TKey}.DisposeAsync"/> will wait for in-flight locks
    /// to drain before force-disposing resources. Default: <c>null</c> (infinite — wait forever).
    /// </summary>
    public TimeSpan? DisposeDrainTimeout { get; set; }

    /// <summary>
    /// Validates all option values. Throws <see cref="ArgumentOutOfRangeException"/> for invalid values.
    /// Called automatically by the <see cref="AsyncKeyedLocker{TKey}"/> constructor.
    /// </summary>
    internal void Validate()
    {
        ThrowIfNotPositive(LockIdleCleanupInterval, nameof(LockIdleCleanupInterval));
        ThrowIfNotPositive(LockIdleCleanupThreshold, nameof(LockIdleCleanupThreshold));
        ThrowIfNotPositive(LongHeldLockLoggingInterval, nameof(LongHeldLockLoggingInterval));
        ThrowIfNotPositive(LongHeldLockThreshold, nameof(LongHeldLockThreshold));

        if (DisposeDrainTimeout.HasValue)
            ThrowIfNotPositive(DisposeDrainTimeout.Value, nameof(DisposeDrainTimeout));
    }

    private static void ThrowIfNotPositive(TimeSpan value, string paramName)
    {
        if (value <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(paramName, value, $"{paramName} must be greater than TimeSpan.Zero.");
    }
}