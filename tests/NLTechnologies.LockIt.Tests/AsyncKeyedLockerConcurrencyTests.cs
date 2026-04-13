using NUnit.Framework;
using System.Collections.Concurrent;

namespace NLTechnologies.LockIt.Tests;

[TestFixture]
public class AsyncKeyedLockerConcurrencyTests
{
    [Test]
    public async Task StressTest_SameKey_100Tasks_NoDataCorruption()
    {
        await using var locker = TestHelper.CreateLocker<string>();
        int sharedCounter = 0;
        const int taskCount = 100;

        var tasks = Enumerable.Range(0, taskCount).Select(_ => Task.Run(async () =>
        {
            await using var lease = await locker.AcquireAsync("stress");

            // Non-atomic read-modify-write that would corrupt without locking
            int local = sharedCounter;
            await Task.Yield();
            sharedCounter = local + 1;
        })).ToArray();

        await Task.WhenAll(tasks);

        Assert.That(sharedCounter, Is.EqualTo(taskCount));
    }

    [Test]
    public async Task StressTest_ManyKeys_Parallel_NoDeadlock()
    {
        await using var locker = TestHelper.CreateLocker<int>();
        const int keyCount = 50;
        const int tasksPerKey = 20;

        var tasks = new List<Task>();
        for (int k = 0; k < keyCount; k++)
        {
            int key = k;
            for (int t = 0; t < tasksPerKey; t++)
            {
                tasks.Add(Task.Run(async () =>
                {
                    await using var lease = await locker.AcquireAsync(key);
                    await Task.Delay(1);
                }));
            }
        }

        await Task.WhenAll(tasks).WaitAsync(TimeSpan.FromSeconds(30));
        Assert.Pass($"All {keyCount * tasksPerKey} tasks completed without deadlock.");
    }

    [Test]
    public async Task StressTest_RapidAcquireRelease_SingleKey()
    {
        await using var locker = TestHelper.CreateLocker<string>();

        for (int i = 0; i < 500; i++)
        {
            await using var lease = await locker.AcquireAsync("rapid");
        }

        Assert.That(locker.DebugGetRefCount("rapid"), Is.EqualTo(0));
    }

    [Test]
    public async Task StressTest_ConcurrentAcquireAndCleanup_NoRace()
    {
        var options = TestHelper.FastCleanupOptions(idleThreshold: TimeSpan.FromMilliseconds(1));
        await using var locker = TestHelper.CreateLocker<int>(options);

        var tasks = new List<Task>();
        for (int i = 0; i < 200; i++)
        {
            int key = i % 10; // 10 distinct keys
            tasks.Add(Task.Run(async () =>
            {
                await using var lease = await locker.AcquireAsync(key);
                await Task.Yield();
            }));

            // Interleave cleanup calls
            if (i % 25 == 0)
            {
                tasks.Add(Task.Run(() => locker.DebugCleanup()));
            }
        }

        await Task.WhenAll(tasks);
        Assert.Pass("Concurrent acquire + cleanup completed without exceptions.");
    }

    [Test]
    public async Task StressTest_MixedTimeoutsAndSuccesses()
    {
        await using var locker = TestHelper.CreateLocker<string>();
        int successes = 0;
        int timeouts = 0;

        // Hold the lock for a period
        var holder = Task.Run(async () =>
        {
            await using var lease = await locker.AcquireAsync("mixed");
            await Task.Delay(300);
        });

        // Fire off many tasks with short timeouts
        var tasks = Enumerable.Range(0, 50).Select(_ => Task.Run(async () =>
        {
            try
            {
                await using var lease = await locker.AcquireAsync("mixed", timeout: TimeSpan.FromMilliseconds(10));
                Interlocked.Increment(ref successes);
            }
            catch (OperationCanceledException)
            {
                Interlocked.Increment(ref timeouts);
            }
        })).ToArray();

        await Task.WhenAll(tasks.Append(holder));

        // Most should time out, but the exact split depends on scheduling
        Assert.That(successes + timeouts, Is.EqualTo(50));
        Assert.That(timeouts, Is.GreaterThan(0), "At least some should have timed out.");
    }

    [Test]
    public async Task StressTest_InterleavedKeysVerifyIsolation()
    {
        await using var locker = TestHelper.CreateLocker<string>();
        var counters = new ConcurrentDictionary<string, int>();
        string[] keys = ["alpha", "beta", "gamma", "delta"];

        var tasks = new List<Task>();
        foreach (var key in keys)
        {
            counters[key] = 0;
            for (int i = 0; i < 100; i++)
            {
                tasks.Add(Task.Run(async () =>
                {
                    await using var lease = await locker.AcquireAsync(key);
                    int val = counters[key];
                    await Task.Yield();
                    counters[key] = val + 1;
                }));
            }
        }

        await Task.WhenAll(tasks);

        foreach (var key in keys)
        {
            Assert.That(counters[key], Is.EqualTo(100), $"Counter for '{key}' should be exactly 100.");
        }
    }
}