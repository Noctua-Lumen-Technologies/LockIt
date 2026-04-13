namespace NLTechnologies.LockIt;

/// <summary>
/// Result of a <see cref="IAsyncKeyedLocker{TKey}.TryAcquireAsync"/> call.
/// Must always be disposed — if <see cref="Acquired"/> is <c>true</c>, disposal releases the lock.
/// If <c>false</c>, disposal is a no-op.
/// <para/>
/// Usage:
/// <code>
/// await using var result = await locker.TryAcquireAsync(key, TimeSpan.FromSeconds(5));
/// if (result.Acquired)
/// {
///     // critical section
/// }
/// </code>
/// </summary>
public readonly struct TryAcquireResult : IAsyncDisposable
{
    private readonly IAsyncDisposable? _lease;

    /// <summary>
    /// Whether the lock was successfully acquired.
    /// </summary>
    public bool Acquired => _lease is not null;

    private TryAcquireResult(IAsyncDisposable? lease)
    {
        _lease = lease;
    }

    /// <summary>
    /// Creates a successful result wrapping the acquired lease.
    /// </summary>
    internal static TryAcquireResult Success(IAsyncDisposable lease) => new(lease);

    /// <summary>
    /// Creates a failed result (lock was not acquired).
    /// </summary>
    internal static TryAcquireResult Failure() => new(null);

    /// <summary>
    /// Disposes the underlying lease if the lock was acquired. No-op otherwise.
    /// </summary>
    public ValueTask DisposeAsync()
    {
        return _lease?.DisposeAsync() ?? default;
    }
}