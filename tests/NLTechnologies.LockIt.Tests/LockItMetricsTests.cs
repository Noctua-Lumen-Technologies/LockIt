using System.Diagnostics.Metrics;
using NUnit.Framework;

namespace NLTechnologies.LockIt.Tests;

[TestFixture]
public class LockItMetricsTests
{
    [Test]
    public async Task Metrics_AcquireAndRelease_CountersIncrement()
    {
        using var metrics = new LockItMetrics();
        long acquired = 0, released = 0, active = 0;

        using var listener = new MeterListener();
        listener.InstrumentPublished = (instrument, meterListener) =>
        {
            if (instrument.Meter.Name == LockItMetrics.MeterName)
                meterListener.EnableMeasurementEvents(instrument);
        };

        listener.SetMeasurementEventCallback<long>((instrument, value, _, _) =>
        {
            switch (instrument.Name)
            {
                case "lockit.locks.acquired": Interlocked.Add(ref acquired, value); break;
                case "lockit.locks.released": Interlocked.Add(ref released, value); break;
                case "lockit.locks.active": Interlocked.Add(ref active, value); break;
            }
        });
        listener.Start();

        await using var locker = TestHelper.CreateLocker<string>(metrics: metrics);

        var lease = await locker.AcquireAsync("m1");
        listener.RecordObservableInstruments();
        Assert.That(Interlocked.Read(ref acquired), Is.EqualTo(1));

        await lease.DisposeAsync();
        listener.RecordObservableInstruments();
        Assert.That(Interlocked.Read(ref released), Is.EqualTo(1));
        Assert.That(Interlocked.Read(ref active), Is.EqualTo(0));
    }

    [Test]
    public async Task Metrics_Timeout_IncrementsTimedOutCounter()
    {
        using var metrics = new LockItMetrics();
        long timedOut = 0;

        using var listener = new MeterListener();
        listener.InstrumentPublished = (instrument, meterListener) =>
        {
            if (instrument.Meter.Name == LockItMetrics.MeterName)
                meterListener.EnableMeasurementEvents(instrument);
        };
        listener.SetMeasurementEventCallback<long>((instrument, value, _, _) =>
        {
            if (instrument.Name == "lockit.locks.timed_out")
                Interlocked.Add(ref timedOut, value);
        });
        listener.Start();

        await using var locker = TestHelper.CreateLocker<string>(metrics: metrics);
        await using var held = await locker.AcquireAsync("mt");

        try { await locker.AcquireAsync("mt", timeout: TimeSpan.FromMilliseconds(30)); }
        catch (OperationCanceledException) { }

        listener.RecordObservableInstruments();
        Assert.That(Interlocked.Read(ref timedOut), Is.EqualTo(1));
    }

    [Test]
    public async Task Metrics_Cleanup_IncrementsRemovedCounter()
    {
        using var metrics = new LockItMetrics();
        long removed = 0;

        using var listener = new MeterListener();
        listener.InstrumentPublished = (instrument, meterListener) =>
        {
            if (instrument.Meter.Name == LockItMetrics.MeterName)
                meterListener.EnableMeasurementEvents(instrument);
        };
        listener.SetMeasurementEventCallback<long>((instrument, value, _, _) =>
        {
            if (instrument.Name == "lockit.cleanup.removed")
                Interlocked.Add(ref removed, value);
        });
        listener.Start();

        var options = TestHelper.FastCleanupOptions(idleThreshold: TimeSpan.FromMilliseconds(1));
        await using var locker = TestHelper.CreateLocker<string>(options, metrics: metrics);

        var lease = await locker.AcquireAsync("mc");
        await lease.DisposeAsync();
        await Task.Delay(50);
        locker.DebugCleanup();

        listener.RecordObservableInstruments();
        Assert.That(Interlocked.Read(ref removed), Is.EqualTo(1));
    }

    [Test]
    public void Metrics_Dispose_DoesNotThrow()
    {
        var metrics = new LockItMetrics();
        metrics.Dispose();
        metrics.Dispose(); // Idempotent

        Assert.Pass();
    }
}