namespace Coworking.Domain.Common;

/// <summary>
/// Base class for all domain entities: everything that is stored in its own table
/// and identified by a primary key.
/// </summary>
/// <remarks>
/// The Class diagram keeps ids as attributes (clanID, rezervacijaID...);
/// here they are unified into one <see cref="Id"/> property.
/// </remarks>
public abstract class Entity
{
    /// <summary>
    /// Primary key. It is 0 until the entity is saved; then PostgreSQL generates the value
    /// and EF Core writes it back here. Private setter: application code never chooses ids.
    /// </summary>
    public int Id { get; private set; }
}
