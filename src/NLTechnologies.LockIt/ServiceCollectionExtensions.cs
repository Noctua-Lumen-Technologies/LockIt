using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace NLTechnologies.LockIt;

/// <summary>
/// Extension methods for registering LockIt services with dependency injection.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers <see cref="IAsyncKeyedLockerFactory"/>, <see cref="LockItMetrics"/> and their
    /// default implementations as singleton services.
    /// </summary>
    public static IServiceCollection AddLockIt(this IServiceCollection services)
    {
        services.TryAddSingleton<LockItMetrics>();
        services.TryAddSingleton(TimeProvider.System);
        services.TryAddSingleton<IAsyncKeyedLockerFactory, AsyncKeyedLockerFactory>();
        return services;
    }
}