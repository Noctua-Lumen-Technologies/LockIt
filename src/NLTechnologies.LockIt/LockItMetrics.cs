using System.Diagnostics.Metrics;

namespace NLTechnologies.LockIt;

/// <summary>
/// Holds all <see cref="System.Diagnostics.Metrics"/> instruments for the LockIt library.
/// Consumers can observe these via OpenTelemetry, Prometheus exporters, or any
/// <see cref="MeterListener"/>-based tool.
/// <para/>
/// Meter name: <c>NLTechnologies.LockIt</c>
/// </summary>
public sealed class LockItMetrics : IDisposable
{
    /// <summary>
    /// The meter name used by LockIt. Use this to subscribe in OpenTelemetry:
    /// <c>builder.AddMeter(LockItMetrics.MeterName)</c>
    /// </summary>
    public const string MeterName = "NLTechnologies.LockIt";

    private readonly Meter _meter;

    internal Counter<long> LocksAcquired { get; }
    internal Counter<long> LocksReleased { get; }
    internal Counter<long> LocksTimedOut { get; }
    internal UpDownCounter<long> LocksActive { get; }
    internal Histogram<double> ContentionTime { get; }
    internal Counter<long> CleanupRemoved { get; }

    /// <summary>
    /// Constructs a new <see cref="LockItMetrics"/> instance using the provided <see cref="IMeterFactory"/>,
    /// or creates a standalone <see cref="Meter"/> if null.
    /// </summary>
    /// <param name="meterFactory">Optional meter factory for DI integration.</param>
    public LockItMetrics(IMeterFactory? meterFactory = null)
    {
        _meter = meterFactory?.Create(MeterName) ?? new Meter(MeterName);

        LocksAcquired = _meter.CreateCounter<long>("lockit.locks.acquired", "locks", "Total successful lock acquisitions.");
        LocksReleased = _meter.CreateCounter<long>("lockit.locks.released", "locks", "Total lock releases.");
        LocksTimedOut = _meter.CreateCounter<long>("lockit.locks.timed_out", "locks", "Total acquisition timeouts.");
        LocksActive = _meter.CreateUpDownCounter<long>("lockit.locks.active", "locks", "Currently held locks.");
        ContentionTime = _meter.CreateHistogram<double>("lockit.locks.contention_time", "ms", "Time spent waiting to acquire a lock.");
        CleanupRemoved = _meter.CreateCounter<long>("lockit.cleanup.removed", "locks", "Idle locks removed by cleanup.");
    }

    /// <inheritdoc/>
    public void Dispose() => _meter.Dispose();
}