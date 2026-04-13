namespace NLTechnologies.LockIt;

/// <summary>
/// Represents a composite key for locking operations based on an entity type and its identifier.
/// Useful for serializing access to operations on a specific entity instance in concurrent scenarios.
/// </summary>
/// <typeparam name="TId">
/// The type of the identifier for the entity (e.g., <see cref="Guid"/>, <see cref="int"/>, <see cref="string"/>).
/// Must be a non-nullable type.
/// </typeparam>
/// <remarks>
/// Initializes a new instance of the <see cref="EntityLockKey{TId}"/> struct with the specified entity type and identifier.
/// </remarks>
/// <param name="entityType">The type of the entity. Cannot be <c>null</c>.</param>
/// <param name="entityId">The identifier of the entity instance.</param>
/// <exception cref="ArgumentNullException">Thrown if <paramref name="entityType"/> is <c>null</c>.</exception>
public readonly struct EntityLockKey<TId>(Type entityType, TId entityId) : IEquatable<EntityLockKey<TId>>
    where TId : notnull
{
    /// <summary>
    /// Gets the type of the entity to lock on.
    /// </summary>
    public Type EntityType { get; } = entityType ?? throw new ArgumentNullException(nameof(entityType));

    /// <summary>
    /// Gets the identifier of the entity instance.
    /// </summary>
    public TId EntityId { get; } = entityId;

    /// <summary>
    /// Determines whether this instance is equal to another <see cref="EntityLockKey{TId}"/>.
    /// </summary>
    /// <param name="other">The other key to compare against.</param>
    /// <returns><c>true</c> if the entity type and identifier match; otherwise, <c>false</c>.</returns>
    public bool Equals(EntityLockKey<TId> other) =>
        EntityType == other.EntityType && EqualityComparer<TId>.Default.Equals(EntityId, other.EntityId);

    /// <summary>
    /// Determines whether this instance is equal to a specified object.
    /// </summary>
    /// <param name="obj">The object to compare with.</param>
    /// <returns><c>true</c> if the object is an <see cref="EntityLockKey{TId}"/> with the same type and identifier; otherwise, <c>false</c>.</returns>
    public override bool Equals(object? obj) => obj is EntityLockKey<TId> other && Equals(other);

    /// <summary>
    /// Returns a hash code for this instance, combining the entity type and identifier.
    /// </summary>
    /// <returns>A hash code suitable for use in hashing algorithms and data structures like dictionaries and hash sets.</returns>
    public override int GetHashCode() => HashCode.Combine(EntityType, EntityId);

    /// <summary>
    /// Returns a string representation of the key in the format "EntityTypeName:EntityId".
    /// </summary>
    /// <returns>A string representing the entity type and identifier.</returns>
    public override string ToString() => $"{EntityType.Name}:{EntityId}";

    /// <inheritdoc/>
    public static bool operator ==(EntityLockKey<TId> left, EntityLockKey<TId> right)
    {
        return left.Equals(right);
    }

    /// <inheritdoc/>
    public static bool operator !=(EntityLockKey<TId> left, EntityLockKey<TId> right)
    {
        return !(left == right);
    }
}
