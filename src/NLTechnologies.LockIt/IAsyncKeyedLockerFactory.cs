namespace NLTechnologies.LockIt;

/// <summary>
/// Factory for creating new <see cref="IAsyncKeyedLocker{TKey}"/> instances.
/// </summary>
public interface IAsyncKeyedLockerFactory
{
    /// <summary>
    /// Creates a new independent <see cref="IAsyncKeyedLocker{TKey}"/> instance.
    /// </summary>
    IAsyncKeyedLocker<TKey> Create<TKey>(AsyncKeyedLockerOptions? options = null) where TKey : notnull;
}
