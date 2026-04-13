using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace NLTechnologies.LockIt;

/// <summary>
/// Provides asynchronous, per-key locking to serialize operations for a given key.
/// <para/>
/// <b>Lock lifecycle:</b>
/// <list type="number">
/// <item>Acquired — <c>GetOrAdd</c> obtains or reuses the <c>LockRef</c> for the key.</item>
/// <item>Locked — semaphore is held; the critical section is executing.</item>
/// <item>Released — semaphore is freed; the <c>LockRef</c> remains in the dictionary for reuse.</item>
/// <item>Idle — no tasks hold or wait on the key; the <c>LockRef</c> stays available for re-acquisition.</item>
/// <item>Cleaned up — after the idle threshold is exceeded, the cleanup timer removes and disposes the <c>LockRef</c>.</item>
/// </list>
/// </summary>
/// <typeparam name="TKey">Type of keys used for locking (e.g. composite key struct).</typeparam>
public sealed class AsyncKeyedLocker<TKey> : IAsyncKeyedLocker<TKey> where TKey : notnull
{
    #region Internal types

    private sealed class LockRef(long nowTicks)
    {
        public SemaphoreSlim Semaphore { get; } = new SemaphoreSlim(1, 1);
        public int RefCount;
        public long LastUsedTicks = nowTicks;
        public long AcquiredAtTicks;
    }

    private sealed class Releaser(AsyncKeyedLocker<TKey> parent, AsyncKeyedLocker<TKey>.LockRef lockRef) : IDisposable
    {
        private int _disposed;
        private readonly AsyncKeyedLocker<TKey> _parent = parent ?? throw new ArgumentNullException(nameof(parent));
        private readonly LockRef _lockRef = lockRef ?? throw new ArgumentNullException(nameof(lockRef));

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) == 0)
            {
                _parent.Release(_lockRef);
            }
        }
    }

    private sealed class AsyncReleaserWrapper(IDisposable releaser) : IAsyncDisposable
    {
        private IDisposable? _releaser = releaser ?? throw new ArgumentNullException(nameof(releaser));

        public ValueTask DisposeAsync()
        {
            var r = Interlocked.Exchange(ref _releaser, null);
            r?.Dispose();
            return default;
        }
    }

    #endregion

    #region Fields / ctor

    private readonly ConcurrentDictionary<TKey, LockRef> _locks = new();
    private int _disposed;
    private readonly TaskCompletionSource _drainComplete = new(TaskCreationOptions.RunContinuationsAsynchronously);

    private readonly TimeSpan _lockIdleCleanupThreshold;
    private readonly TimeSpan _longHeldThreshold;
    private readonly TimeSpan? _disposeDrainTimeout;
    private readonly Timer _cleanupTimer;
    private readonly Timer _longHeldLockTimer;
    private readonly ILogger<AsyncKeyedLocker<TKey>> _logger;
    private readonly TimeProvider _timeProvider;
    private readonly LockItMetrics _metrics;

    internal int DebugGetRefCount(TKey key) => _locks.TryGetValue(key, out var lr) ? lr.RefCount : -1;
    internal bool DebugHasKey(TKey key) => _locks.ContainsKey(key);
    internal void DebugCleanup() => CleanupStaleLocks();

    /// <summary>
    /// Construct a new AsyncKeyedLocker.
    /// </summary>
    /// <param name="logger">Typed logger used for diagnostics. Required.</param>
    /// <param name="options">Optional configuration values. Provide null to use defaults.</param>
    /// <param name="timeProvider">Optional time provider for testability. Defaults to <see cref="TimeProvider.System"/>.</param>
    /// <param name="metrics">Optional shared metrics instance. Provide null to create a standalone instance.</param>
    public AsyncKeyedLocker(
        ILogger<AsyncKeyedLocker<TKey>> logger,
        AsyncKeyedLockerOptions? options = null,
        TimeProvider? timeProvider = null,
        LockItMetrics? metrics = null)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _timeProvider = timeProvider ?? TimeProvider.System;
        _metrics = metrics ?? new LockItMetrics();

        options ??= new AsyncKeyedLockerOptions();
        options.Validate();

        _lockIdleCleanupThreshold = options.LockIdleCleanupThreshold;
        _longHeldThreshold = options.LongHeldLockThreshold;
        _disposeDrainTimeout = options.DisposeDrainTimeout;

        _cleanupTimer = new Timer(_ =>
        {
            try { CleanupStaleLocks(); }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[AsyncKeyedLocker] Exception during CleanupStaleLocks timer callback.");
            }
        }, null, options.LockIdleCleanupInterval, options.LockIdleCleanupInterval);

        _longHeldLockTimer = new Timer(_ =>
        {
            try { CheckLongHeldLocks(); }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[AsyncKeyedLocker] Exception during CheckLongHeldLocks timer callback.");
            }
        }, null, options.LongHeldLockLoggingInterval, options.LongHeldLockLoggingInterval);
    }

    #endregion

    #region Acquire / TryAcquire / Release

    private async Task<IDisposable> InternalAcquireAsync(TKey key, TimeSpan? timeout = null, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) == 1, this);
        ArgumentNullException.ThrowIfNull(key);

        var nowTicks = _timeProvider.GetTimestamp();
        LockRef lockRef = _locks.GetOrAdd(key, _ => new LockRef(nowTicks));
        Interlocked.Increment(ref lockRef.RefCount);

        long startTimestamp = _timeProvider.GetTimestamp();

        try
        {
            using CancellationTokenSource? cts = timeout.HasValue
                ? CancellationTokenSource.CreateLinkedTokenSource(cancellationToken)
                : null;

            if (timeout.HasValue && cts != null)
                cts.CancelAfter(timeout.Value);

            CancellationToken tokenToUse = cts?.Token ?? cancellationToken;

            await lockRef.Semaphore.WaitAsync(tokenToUse).ConfigureAwait(false);

            Volatile.Write(ref lockRef.AcquiredAtTicks, _timeProvider.GetTimestamp());
        }
        catch (OperationCanceledException)
        {
            Interlocked.Decrement(ref lockRef.RefCount);
            _metrics.LocksTimedOut.Add(1);
            TrySignalDrainComplete();
            throw;
        }
        catch
        {
            Interlocked.Decrement(ref lockRef.RefCount);
            TrySignalDrainComplete();
            throw;
        }

        double contentionMs = _timeProvider.GetElapsedTime(startTimestamp).TotalMilliseconds;
        _metrics.LocksAcquired.Add(1);
        _metrics.LocksActive.Add(1);
        _metrics.ContentionTime.Record(contentionMs);

        if (contentionMs > 200 && _logger.IsEnabled(LogLevel.Debug))
        {
            _logger.LogDebug("[AsyncKeyedLocker] Lock acquisition for key '{Key}' took {ElapsedMs:N0} ms.", key, contentionMs);
        }

        return new Releaser(this, lockRef);
    }

    /// <inheritdoc/>
    public async Task<IAsyncDisposable> AcquireAsync(TKey key, TimeSpan? timeout = null, CancellationToken cancellationToken = default)
    {
        IDisposable releaser = await InternalAcquireAsync(key, timeout, cancellationToken).ConfigureAwait(false);
        return new AsyncReleaserWrapper(releaser);
    }

    /// <inheritdoc/>
    public async Task<TryAcquireResult> TryAcquireAsync(TKey key, TimeSpan timeout, CancellationToken cancellationToken = default)
    {
        try
        {
            var lease = await AcquireAsync(key, timeout, cancellationToken).ConfigureAwait(false);
            return TryAcquireResult.Success(lease);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return TryAcquireResult.Failure();
        }
    }

    /// <inheritdoc/>
    public int GetQueueDepth(TKey key)
    {
        return _locks.TryGetValue(key, out var lr) ? Math.Max(0, Volatile.Read(ref lr.RefCount)) : 0;
    }

    private void Release(LockRef lockRef)
    {
        try
        {
            lockRef.Semaphore.Release();
        }
        catch (SemaphoreFullException e)
        {
            _logger.LogWarning(e, "[AsyncKeyedLocker] SemaphoreFullException encountered when releasing semaphore.");
        }
        catch (ObjectDisposedException)
        {
            // Semaphore was already disposed during forced drain-timeout disposal.
            // Safe to swallow — the locker is shutting down.
        }

        Volatile.Write(ref lockRef.AcquiredAtTicks, 0);
        Volatile.Write(ref lockRef.LastUsedTicks, _timeProvider.GetTimestamp());
        Interlocked.Decrement(ref lockRef.RefCount);

        _metrics.LocksReleased.Add(1);
        _metrics.LocksActive.Add(-1);

        TrySignalDrainComplete();
    }

    private void TrySignalDrainComplete()
    {
        if (Volatile.Read(ref _disposed) != 1)
            return;

        foreach (var kv in _locks)
        {
            if (Volatile.Read(ref kv.Value.RefCount) > 0)
                return;
        }

        _drainComplete.TrySetResult();
    }

    #endregion

    #region Cleanup & diagnostics

    private void CleanupStaleLocks()
    {
        long nowTicks = _timeProvider.GetTimestamp();

        foreach (var kv in _locks)
        {
            var key = kv.Key;
            var lr = kv.Value;

            if (Volatile.Read(ref lr.AcquiredAtTicks) != 0)
                continue;

            if (Volatile.Read(ref lr.RefCount) > 0)
                continue;

            var idleDuration = _timeProvider.GetElapsedTime(Volatile.Read(ref lr.LastUsedTicks), nowTicks);
            if (idleDuration <= _lockIdleCleanupThreshold)
                continue;

            if (((ICollection<KeyValuePair<TKey, LockRef>>)_locks).Remove(new KeyValuePair<TKey, LockRef>(key, lr)))
            {
                if (Volatile.Read(ref lr.RefCount) > 0)
                {
                    _locks.TryAdd(key, lr);
                }
                else
                {
                    lr.Semaphore.Dispose();
                    _metrics.CleanupRemoved.Add(1);
                    if (_logger.IsEnabled(LogLevel.Debug))
                    {
                        _logger.LogDebug("[AsyncKeyedLocker] Removed idle lock '{Key}' after {IdleSeconds:N0} seconds.",
                            key, idleDuration.TotalSeconds);
                    }
                }
            }
        }
    }

    private void CheckLongHeldLocks()
    {
        long nowTicks = _timeProvider.GetTimestamp();

        foreach (var kv in _locks)
        {
            var key = kv.Key;
            var lr = kv.Value;

            long acquiredTicks = Volatile.Read(ref lr.AcquiredAtTicks);
            if (acquiredTicks == 0)
                continue;

            var heldDuration = _timeProvider.GetElapsedTime(acquiredTicks, nowTicks);
            if (heldDuration > _longHeldThreshold)
            {
                if (_logger.IsEnabled(LogLevel.Warning))
                {
                    _logger.LogWarning("[AsyncKeyedLocker] Lock for key '{Key}' has been held for {HeldSeconds:N1} s, threshold {ThresholdSeconds:N1} s.",
                        key, heldDuration.TotalSeconds, _longHeldThreshold.TotalSeconds);
                }
            }
        }
    }

    #endregion

    #region Disposal

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 1)
        {
            await _drainComplete.Task.ConfigureAwait(false);
            return;
        }

        await _cleanupTimer.DisposeAsync().ConfigureAwait(false);
        await _longHeldLockTimer.DisposeAsync().ConfigureAwait(false);

        TrySignalDrainComplete();

        _logger.LogDebug("[AsyncKeyedLocker] Disposal initiated, waiting for in-flight locks to drain.");

        if (_disposeDrainTimeout.HasValue)
        {
            Task drainTask = _drainComplete.Task;
            if (await Task.WhenAny(drainTask, Task.Delay(_disposeDrainTimeout.Value)).ConfigureAwait(false) != drainTask)
            {
                if (_logger.IsEnabled(LogLevel.Warning))
                {
                    _logger.LogWarning("[AsyncKeyedLocker] Drain timeout of {TimeoutSeconds:N1} s exceeded. Force-disposing with {ActiveKeys} active key(s).",
                        _disposeDrainTimeout.Value.TotalSeconds, _locks.Count(kv => Volatile.Read(ref kv.Value.RefCount) > 0));
                }
            }
        }
        else
        {
            await _drainComplete.Task.ConfigureAwait(false);
        }

        _logger.LogDebug("[AsyncKeyedLocker] Disposing resources.");

        foreach (var kv in _locks)
        {
            kv.Value.Semaphore.Dispose();
        }

        _locks.Clear();
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        DisposeAsync().AsTask().GetAwaiter().GetResult();
    }

    #endregion
}
