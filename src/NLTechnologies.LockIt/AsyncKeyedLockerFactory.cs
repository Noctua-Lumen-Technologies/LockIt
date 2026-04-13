using Microsoft.Extensions.Logging;

namespace NLTechnologies.LockIt;

/// <summary>
/// Constructs new <see cref="AsyncKeyedLocker{TKey}"/> instances with shared logging, metrics and time provider.
/// </summary>
/// <param name="loggerFactory">Logger factory for creating typed loggers.</param>
/// <param name="metrics">Optional shared metrics instance.</param>
/// <param name="timeProvider">Optional time provider.</param>
public class AsyncKeyedLockerFactory(
    ILoggerFactory loggerFactory,
    LockItMetrics? metrics = null,
    TimeProvider? timeProvider = null) : IAsyncKeyedLockerFactory
{
    private readonly ILoggerFactory _loggerFactory = loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory));
    private readonly LockItMetrics _metrics = metrics ?? new LockItMetrics();
    private readonly TimeProvider _timeProvider = timeProvider ?? TimeProvider.System;

    /// <inheritdoc/>
    public IAsyncKeyedLocker<TKey> Create<TKey>(AsyncKeyedLockerOptions? options = null) where TKey : notnull
    {
        ILogger<AsyncKeyedLocker<TKey>> logger = _loggerFactory.CreateLogger<AsyncKeyedLocker<TKey>>();
        return new AsyncKeyedLocker<TKey>(logger, options, _timeProvider, _metrics);
    }
}
