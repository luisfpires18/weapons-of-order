namespace WeaponsOfOrder.Infrastructure.Gameplay;

/// <summary>
/// One Unit a player owns.
/// </summary>
/// <remarks>
/// An instance of a definition, not a copy of one. <see cref="DefinitionKey"/> points at an
/// entry in <c>content/units.json</c>, and everything the definition says — name, kingdom,
/// tier, maximum armour, Mounted — is resolved from that content on every read. Copying those
/// values in here would freeze them at the moment the row was written and make an ordinary
/// content edit a data migration.
/// <para>
/// There is deliberately no uniqueness on owner + definition. Canon allows a Regular Unit to
/// exist in multiple copies, so the only thing held unique per account is
/// <see cref="StarterGrantKey"/>, which is what makes the temporary starter grant idempotent
/// without also making duplicates impossible.
/// </para>
/// </remarks>
public sealed class PlayerUnit
{
    public Guid Id { get; set; }

    public Guid OwnerUserId { get; set; }

    /// <summary>Stable key of the definition in the Unit content file.</summary>
    public string DefinitionKey { get; set; } = string.Empty;

    /// <summary>
    /// The one-per-account grant that created this Unit, or <see langword="null"/> when it
    /// arrived some other way.
    /// </summary>
    /// <remarks>
    /// Unique per account when set. A Unit from a future recruitment path leaves it null, and
    /// The partial index's filter excludes those rows, so an account may hold any number of
    /// copies of the same definition.
    /// </remarks>
    public string? StarterGrantKey { get; set; }

    public PlayerUnitOrigin Origin { get; set; }

    public DateTimeOffset AcquiredAt { get; set; }
}
