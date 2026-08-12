namespace WeaponsOfOrder.Infrastructure.Gameplay;

/// <summary>
/// A weapon a player has put in a Unit's hands.
/// </summary>
/// <remarks>
/// One row per equipped item rather than one row per occupied slot. That is what lets a
/// canonical two-slot weapon — a Bow, when bows exist — occupy the whole loadout as a single
/// physical object instead of being written down twice.
/// <para>
/// The two slot flags say which hands the weapon is in: one of them for a 1-slot weapon,
/// both for a 2-slot weapon. Three database guarantees hold the rules up, so two racing
/// requests cannot talk their way past them:
/// </para>
/// <list type="bullet">
/// <item>the primary key is <see cref="ItemId"/>, so one physical item is equipped in at most
/// one place, on at most one Unit;</item>
/// <item>a filtered unique index per slot flag, so a Unit's first and second hands each hold
/// at most one weapon;</item>
/// <item>composite foreign keys carrying <see cref="OwnerUserId"/>, so the item and the Unit
/// in one row cannot belong to different accounts.</item>
/// </list>
/// </remarks>
public sealed class EquippedWeapon
{
    /// <summary>The owned item. Also the primary key: an item is equipped once or not at all.</summary>
    public Guid ItemId { get; set; }

    public Guid PlayerUnitId { get; set; }

    /// <summary>
    /// The account both the item and the Unit belong to. Denormalised on purpose: it is what
    /// the composite foreign keys check against, which makes cross-account equipment a
    /// database impossibility rather than a rule the service has to remember.
    /// </summary>
    public Guid OwnerUserId { get; set; }

    public bool OccupiesFirstSlot { get; set; }

    public bool OccupiesSecondSlot { get; set; }

    public DateTimeOffset EquippedAt { get; set; }

    /// <summary>The slot numbers this weapon occupies, in order.</summary>
    public IEnumerable<int> Slots
    {
        get
        {
            if (OccupiesFirstSlot)
            {
                yield return Loadout.FirstSlot;
            }

            if (OccupiesSecondSlot)
            {
                yield return Loadout.SecondSlot;
            }
        }
    }
}
