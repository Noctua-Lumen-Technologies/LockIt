using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NUnit.Framework;

namespace NLTechnologies.LockIt.Tests;

[TestFixture]
public class FactoryAndDependencyInjectionTests
{
    [Test]
    public void Factory_Create_ReturnsNewInstance()
    {
        var loggerFactory = new LoggerFactory();
        var factory = new AsyncKeyedLockerFactory(loggerFactory);

        using var locker1 = factory.Create<string>();
        using var locker2 = factory.Create<string>();

        Assert.That(locker1, Is.Not.SameAs(locker2));
    }

    [Test]
    public void Factory_Create_ReturnsIAsyncKeyedLocker()
    {
        var loggerFactory = new LoggerFactory();
        var factory = new AsyncKeyedLockerFactory(loggerFactory);

        var locker = factory.Create<string>();

        Assert.That(locker, Is.InstanceOf<IAsyncKeyedLocker<string>>());
        locker.Dispose();
    }

    [Test]
    public void Factory_Create_WithOptions_AppliesOptions()
    {
        var loggerFactory = new LoggerFactory();
        var factory = new AsyncKeyedLockerFactory(loggerFactory);
        var options = new AsyncKeyedLockerOptions { LockIdleCleanupThreshold = TimeSpan.FromMinutes(99) };

        var locker = factory.Create<int>(options);

        Assert.That(locker, Is.Not.Null);
        locker.Dispose();
    }

    [Test]
    public void Factory_NullLoggerFactory_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => new AsyncKeyedLockerFactory(null!));
    }

    [Test]
    public void AddLockIt_RegistersFactory_AsSingleton()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddLockIt();

        using var provider = services.BuildServiceProvider();

        var factory1 = provider.GetRequiredService<IAsyncKeyedLockerFactory>();
        var factory2 = provider.GetRequiredService<IAsyncKeyedLockerFactory>();

        Assert.That(factory1, Is.SameAs(factory2), "Should be registered as singleton.");
    }

    [Test]
    public void AddLockIt_RegistersMetrics_AsSingleton()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddLockIt();

        using var provider = services.BuildServiceProvider();

        var metrics1 = provider.GetRequiredService<LockItMetrics>();
        var metrics2 = provider.GetRequiredService<LockItMetrics>();

        Assert.That(metrics1, Is.SameAs(metrics2));
    }

    [Test]
    public void AddLockIt_FactoryCreatesWorkingLocker()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddLockIt();

        using var provider = services.BuildServiceProvider();
        var factory = provider.GetRequiredService<IAsyncKeyedLockerFactory>();
        var locker = factory.Create<string>();

        Assert.That(locker, Is.Not.Null);
        locker.Dispose();
    }

    [Test]
    public void AddLockIt_CalledMultipleTimes_DoesNotThrow()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddLockIt();
        services.AddLockIt();

        using var provider = services.BuildServiceProvider();
        var factory = provider.GetRequiredService<IAsyncKeyedLockerFactory>();

        Assert.That(factory, Is.Not.Null);
    }

    [Test]
    public void Constructor_NullLogger_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => new AsyncKeyedLocker<string>(null!));
    }

    [Test]
    public void Constructor_DefaultOptions_DoesNotThrow()
    {
        var logger = new LoggerFactory().CreateLogger<AsyncKeyedLocker<string>>();

        using var locker = new AsyncKeyedLocker<string>(logger);

        Assert.That(locker, Is.Not.Null);
    }
}