using NUnit.Framework;

namespace NLTechnologies.LockIt.Tests;

[TestFixture]
public class AsyncKeyedLockerKeyTypeTests
{
    [Test]
    public async Task Key_String_Works()
    {
        await using var locker = TestHelper.CreateLocker<string>();
        await using var lease = await locker.AcquireAsync("hello");
        Assert.That(lease, Is.Not.Null);
    }

    [Test]
    public async Task Key_Int_Works()
    {
        await using var locker = TestHelper.CreateLocker<int>();
        await using var lease = await locker.AcquireAsync(42);
        Assert.That(lease, Is.Not.Null);
    }

    [Test]
    public async Task Key_Guid_Works()
    {
        await using var locker = TestHelper.CreateLocker<Guid>();
        await using var lease = await locker.AcquireAsync(Guid.NewGuid());
        Assert.That(lease, Is.Not.Null);
    }

    [Test]
    public async Task Key_Long_Works()
    {
        await using var locker = TestHelper.CreateLocker<long>();
        await using var lease = await locker.AcquireAsync(999_999_999L);
        Assert.That(lease, Is.Not.Null);
    }

    [Test]
    public async Task Key_ValueTuple_Works()
    {
        await using var locker = TestHelper.CreateLocker<(string, int)>();

        await using var lease1 = await locker.AcquireAsync(("Entity", 1));
        Assert.That(lease1, Is.Not.Null);
    }

    [Test]
    public async Task Key_ValueTuple_DifferentValues_RunInParallel()
    {
        await using var locker = TestHelper.CreateLocker<(string, int)>();
        var bothRunning = new CountdownEvent(2);
        var proceed = new ManualResetEventSlim(false);

        var t1 = Task.Run(async () =>
        {
            await using var lease = await locker.AcquireAsync(("A", 1));
            bothRunning.Signal();
            proceed.Wait();
        });
        var t2 = Task.Run(async () =>
        {
            await using var lease = await locker.AcquireAsync(("B", 2));
            bothRunning.Signal();
            proceed.Wait();
        });

        bool parallel = bothRunning.Wait(TimeSpan.FromSeconds(5));
        proceed.Set();
        await Task.WhenAll(t1, t2);

        Assert.That(parallel, Is.True);
    }

    [Test]
    public async Task Key_EntityLockKeyGuid_SerializesSameEntity()
    {
        await using var locker = TestHelper.CreateLocker<EntityLockKey<Guid>>();
        var id = Guid.NewGuid();
        int counter = 0;

        var tasks = Enumerable.Range(0, 30).Select(_ => Task.Run(async () =>
        {
            var key = new EntityLockKey<Guid>(typeof(string), id);
            await using var lease = await locker.AcquireAsync(key);
            int v = counter;
            await Task.Yield();
            counter = v + 1;
        })).ToArray();

        await Task.WhenAll(tasks);

        Assert.That(counter, Is.EqualTo(30));
    }
}