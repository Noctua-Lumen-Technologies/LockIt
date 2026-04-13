namespace NLTechnologies.LockIt;

/// <summary>
/// Abstraction for an async per-key locker instance. Implement or mock this interface
/// in consumer test suites to decouple from the concrete <see cref="AsyncKeyedLocker{TKey}"/>.
/// </summary>
/// <typeparam name="TKey">Type of keys used for locking.</typeparam>
public interface IAsyncKeyedLocker<TKey> : IAsyncDisposable, IDisposable where TKey : notnull
{
    /// <summary>
    /// Acquire an asynchronous lease for the given key.
    /// </summary>
    Task<IAsyncDisposable> AcquireAsync(TKey key, TimeSpan? timeout = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Try to acquire an asynchronous lease. Returns a result without throwing on timeout.
    /// </summary>
    Task<TryAcquireResult> TryAcquireAsync(TKey key, TimeSpan timeout, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the number of tasks currently holding or waiting on the lock for the given key.
    /// </summary>
    int GetQueueDepth(TKey key);
}