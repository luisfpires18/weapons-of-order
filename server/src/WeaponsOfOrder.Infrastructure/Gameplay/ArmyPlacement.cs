namespace WeaponsOfOrder.Infrastructure.Gameplay;

/// <summary>Whether a Unit starts a battle on the battlefield or waits behind it.</summary>
/// <remarks>
/// Canon's structural distinction: the Deployment Limit governs how many may be active at once,
/// and the Army Limit how many the army holds in total.
/// </remarks>
public enum ArmyRole
{
    /// <summary>Standing on the battlefield when the battle begins, at its own hex.</summary>
    Active = 0,

    /// <summary>Waiting off-board in an ordered queue, to enter when an active slot opens.</summary>
    Reserve = 1,
}

/// <summary>
/// Where one of a player's Units stands in that player's army.
/// </summary>
/// <remarks>
/// An account has exactly one army, and these rows are it. There is no parent row: a placement is
/// identified by the account and the Unit, which is also what makes "a Unit cannot appear twice"
/// the primary key rather than a check the service has to remember.
/// <para>
/// Four database guarantees hold the deployment rules up, so two requests arriving at once cannot
/// talk their way past them:
/// </para>
/// <list type="bullet">
/// <item>the primary key is the account and the Unit, so one Unit is placed once or not at all;</item>
/// <item>a filtered unique index on the hex, so two active Units cannot share one;</item>
/// <item>a filtered unique index on the queue position, so the reserve order is a real order;</item>
/// <item>a composite foreign key carrying <see cref="OwnerUserId"/>, so a placement pairing one
/// account with another account's Unit cannot be written at all.</item>
/// </list>
/// <para>
/// Weapons are deliberately not referenced here. What a Unit is holding is its loadout, which
/// lives on <see cref="EquippedWeapon"/> and is resolved when the battle is built — so changing a
/// sword does not mean re-deploying an army.
/// </para>
/// </remarks>
public sealed class ArmyPlacement
{
    public Guid OwnerUserId { get; set; }

    public Guid PlayerUnitId { get; set; }

    public ArmyRole Role { get; set; }

    /// <summary>The hex column for an active Unit; null for a reserve.</summary>
    /// <remarks>
    /// Offset hex coordinates, in the player's own deployment half. The database checks the bounds
    /// as well as the API, because a row outside them would be an army the simulator refuses to
    /// field and there would be no way back except an edit.
    /// </remarks>
    public int? HexColumn { get; set; }

    /// <summary>The hex row for an active Unit; null for a reserve.</summary>
    public int? HexRow { get; set; }

    /// <summary>
    /// Queue position for a reserve, from zero; null for an active Unit.
    /// </summary>
    /// <remarks>
    /// The order the player chose, and the order reinforcements are called in. It also decides the
    /// rear-column hex a reserve enters through, which is why it has to be an order rather than a
    /// set — two reserves sharing a position would make arrival order arbitrary.
    /// </remarks>
    public int? ReserveOrder { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }
}
