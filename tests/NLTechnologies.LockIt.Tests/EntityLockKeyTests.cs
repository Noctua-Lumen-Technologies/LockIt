using NUnit.Framework;

namespace NLTechnologies.LockIt.Tests;

[TestFixture]
public class EntityLockKeyTests
{
    private sealed class FakeEntity;
    private sealed class OtherEntity;

    [Test]
    public void Equals_SameTypeAndId_ReturnsTrue()
    {
        var a = new EntityLockKey<Guid>(typeof(FakeEntity), Guid.Empty);
        var b = new EntityLockKey<Guid>(typeof(FakeEntity), Guid.Empty);

        Assert.That(a.Equals(b), Is.True);
        Assert.That(a == b, Is.True);
    }

    [Test]
    public void Equals_DifferentId_ReturnsFalse()
    {
        var a = new EntityLockKey<Guid>(typeof(FakeEntity), Guid.NewGuid());
        var b = new EntityLockKey<Guid>(typeof(FakeEntity), Guid.NewGuid());

        Assert.That(a.Equals(b), Is.False);
        Assert.That(a != b, Is.True);
    }

    [Test]
    public void Equals_DifferentType_SameId_ReturnsFalse()
    {
        var id = Guid.NewGuid();
        var a = new EntityLockKey<Guid>(typeof(FakeEntity), id);
        var b = new EntityLockKey<Guid>(typeof(OtherEntity), id);

        Assert.That(a.Equals(b), Is.False);
    }

    [Test]
    public void Equals_Object_BoxedSameKey_ReturnsTrue()
    {
        var a = new EntityLockKey<int>(typeof(FakeEntity), 42);
        object b = new EntityLockKey<int>(typeof(FakeEntity), 42);

        Assert.That(a.Equals(b), Is.True);
    }

    [Test]
    public void Equals_Object_DifferentType_ReturnsFalse()
    {
        var a = new EntityLockKey<int>(typeof(FakeEntity), 42);

        Assert.That(a.Equals("not a key"), Is.False);
        Assert.That(a.Equals(null), Is.False);
    }

    [Test]
    public void GetHashCode_SameKeyPair_SameHash()
    {
        var a = new EntityLockKey<string>(typeof(FakeEntity), "abc");
        var b = new EntityLockKey<string>(typeof(FakeEntity), "abc");

        Assert.That(a.GetHashCode(), Is.EqualTo(b.GetHashCode()));
    }

    [Test]
    public void GetHashCode_DifferentKeyPair_DifferentHash()
    {
        var a = new EntityLockKey<string>(typeof(FakeEntity), "abc");
        var b = new EntityLockKey<string>(typeof(FakeEntity), "xyz");

        // Not guaranteed but highly likely
        Assert.That(a.GetHashCode(), Is.Not.EqualTo(b.GetHashCode()));
    }

    [Test]
    public void ToString_ReturnsExpectedFormat()
    {
        var key = new EntityLockKey<int>(typeof(FakeEntity), 99);

        Assert.That(key.ToString(), Is.EqualTo("FakeEntity:99"));
    }

    [Test]
    public void Constructor_NullEntityType_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => new EntityLockKey<int>(null!, 1));
    }

    [Test]
    public void Properties_ReturnConstructorValues()
    {
        var type = typeof(FakeEntity);
        var id = Guid.NewGuid();
        var key = new EntityLockKey<Guid>(type, id);

        Assert.Multiple(() =>
        {
            Assert.That(key.EntityType, Is.EqualTo(type));
            Assert.That(key.EntityId, Is.EqualTo(id));
        });
    }

    [Test]
    public async Task EntityLockKey_WorksAsKeyInLocker()
    {
        await using var locker = TestHelper.CreateLocker<EntityLockKey<Guid>>();
        var id = Guid.NewGuid();
        var key = new EntityLockKey<Guid>(typeof(FakeEntity), id);
        int counter = 0;

        var tasks = Enumerable.Range(0, 50).Select(_ => Task.Run(async () =>
        {
            await using var lease = await locker.AcquireAsync(key);
            int val = counter;
            await Task.Yield();
            counter = val + 1;
        })).ToArray();

        await Task.WhenAll(tasks);

        Assert.That(counter, Is.EqualTo(50));
    }

    [Test]
    public async Task EntityLockKey_DifferentEntities_DifferentTypes_ParallelAccess()
    {
        await using var locker = TestHelper.CreateLocker<EntityLockKey<Guid>>();
        var id = Guid.NewGuid();
        var key1 = new EntityLockKey<Guid>(typeof(FakeEntity), id);
        var key2 = new EntityLockKey<Guid>(typeof(OtherEntity), id);

        var bothRunning = new CountdownEvent(2);
        var proceed = new ManualResetEventSlim(false);

        var t1 = Task.Run(async () =>
        {
            await using var lease = await locker.AcquireAsync(key1);
            bothRunning.Signal();
            proceed.Wait();
        });
        var t2 = Task.Run(async () =>
        {
            await using var lease = await locker.AcquireAsync(key2);
            bothRunning.Signal();
            proceed.Wait();
        });

        bool parallel = bothRunning.Wait(TimeSpan.FromSeconds(5));
        proceed.Set();
        await Task.WhenAll(t1, t2);

        Assert.That(parallel, Is.True, "Different entity types with same ID should run in parallel.");
    }
}