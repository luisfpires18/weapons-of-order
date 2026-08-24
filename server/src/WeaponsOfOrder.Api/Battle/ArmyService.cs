using Microsoft.EntityFrameworkCore;
using WeaponsOfOrder.Api.Content;
using WeaponsOfOrder.Api.Preparation;
using WeaponsOfOrder.Combat;
using WeaponsOfOrder.Infrastructure.Gameplay;
using WeaponsOfOrder.Infrastructure.Persistence;

namespace WeaponsOfOrder.Api.Battle;

/// <summary>
/// One of the player's Units, resolved: who it is, what it holds, what it fights with, and where
/// it stands in the army.
/// </summary>
internal sealed record ArmyMember(
    UnitPayload Unit,
    UnitDefinition Definition,
    CombatantStats Stats,
    ArmyRole? Role,
    Hex? Hex,
    int? ReserveOrder)
{
    public Guid UnitId => Unit.Id;
}

/// <summary>
/// The player's whole army, with the board and the limits it was resolved against.
/// </summary>
/// <remarks>
/// The single thing both the deployment screen and the battle builder read, so what the player is
/// shown and what is actually fought cannot describe different armies.
/// </remarks>
internal sealed record ResolvedArmy(
    IReadOnlyList<ArmyMember> Members,
    Battlefield Battlefield,
    CombatTuning Tuning)
{
    /// <summary>
    /// The Units on the battlefield, in the order the battle will number them.
    /// </summary>
    /// <remarks>
    /// Sorted by hex rather than left in whatever order the rows came back in. Combatant
    /// identifiers are assigned from this order and are the simulator's last-resort tie-break, so
    /// leaving it to the database would make an exactly tied target selection depend on a query
    /// plan.
    /// </remarks>
    public IReadOnlyList<ArmyMember> Active =>
    [
        .. Members
            .Where(member => member.Role == ArmyRole.Active)
            .OrderBy(member => member.Hex!.Value.Column)
            .ThenBy(member => member.Hex!.Value.Row),
    ];

    /// <summary>The Units waiting behind, in the queue order the player chose.</summary>
    public IReadOnlyList<ArmyMember> Reserves =>
    [
        .. Members.Where(member => member.Role == ArmyRole.Reserve).OrderBy(member => member.ReserveOrder),
    ];

    /// <summary>Whether a battle could be fought. Reserves alone would be an army that never turns up.</summary>
    public bool Ready => Active.Count > 0;

    public ArmyPayload ToPayload() => new(
        new BattlefieldPayload(Battlefield.Columns, Battlefield.Rows, Battlefield.HalfColumns),
        new ArmyLimitsPayload(Tuning.ActiveLimit, Tuning.ReserveLimit, Tuning.ArmyLimit),
        [.. Members.Select(ToPayload)],
        Ready);

    private ArmyUnitPayload ToPayload(ArmyMember member) => new(
        member.UnitId,
        member.Unit.DefinitionKey,
        member.Unit.Name,
        member.Unit.Kingdom,
        member.Unit.Tier,
        member.Definition.Mounted,
        [.. member.Unit.Weapons.Select(weapon => new ArmyWeaponPayload(weapon.ItemId, weapon.Name, weapon.Craftsmanship))],
        new CombatStatsPayload(
            member.Stats.Hp,
            member.Stats.Power,
            member.Stats.Defense,
            member.Stats.AttackIntervalSeconds,
            member.Stats.CriticalChance,
            member.Stats.Range,
            member.Stats.Mounted),
        member.Role switch
        {
            ArmyRole.Active => "active",
            ArmyRole.Reserve => "reserve",
            _ => "unplaced",
        },
        member.Hex is { } hex ? new HexPayload(hex.Column, hex.Row) : null,
        member.ReserveOrder,

        // Shown during deployment because it is a real consequence of queue order: a reserve enters
        // through this hex or it waits, and there is no fallback.
        member.ReserveOrder is { } queue
            ? Battlefield.ReserveEntryHex(BattleSide.Player, queue) is var entry
                ? new HexPayload(entry.Column, entry.Row)
                : null
            : null);
}

/// <summary>
/// The player's army: reading it, and replacing it.
/// </summary>
/// <remarks>
/// Every method takes the account id the endpoint read from the session cookie, and every query is
/// filtered by it. The browser names Units and hexes; it never names an owner, and an identifier
/// belonging to somebody else is answered exactly as one that does not exist.
/// <para>
/// The rules this enforces in code are also enforced by the database, which is what makes them
/// hold under two requests arriving at once. See <see cref="ArmyPlacement"/>.
/// </para>
/// </remarks>
internal sealed class ArmyService(
    WeaponsOfOrderDbContext db,
    PreparationService preparation,
    UnitCatalogue units,
    WeaponCatalogue weapons,
    CombatProfiles profiles,
    TimeProvider clock)
{
    private static readonly Battlefield Field = Battlefield.Canonical;

    /// <summary>The caller's Units, their loadouts, their final stats and their places.</summary>
    /// <remarks>
    /// Goes through the preparation service for the roster rather than querying Units again, so
    /// there is one place that resolves a player-owned Unit through the creator's content — and one
    /// place that grants the temporary starter roster to an account seeing it for the first time.
    /// </remarks>
    public async Task<ResolvedArmy> ReadAsync(Guid userId, CancellationToken cancellationToken)
    {
        var roster = await preparation.ListUnitsAsync(userId, cancellationToken);

        var placements = await db.ArmyPlacements
            .AsNoTracking()
            .Where(placement => placement.OwnerUserId == userId)
            .ToListAsync(cancellationToken);

        var placed = placements.ToDictionary(placement => placement.PlayerUnitId);

        return new ResolvedArmy(
            [.. roster.Select(unit => Resolve(unit, placed.GetValueOrDefault(unit.Id)))],
            Field,
            profiles.Tuning);
    }

    /// <summary>
    /// Replaces the caller's army with the one they asked for, or changes nothing.
    /// </summary>
    /// <remarks>
    /// A whole replacement in one transaction. Placing, moving, removing and reordering are all the
    /// same operation from here, and there is no sequence of partial writes that can leave a
    /// deployment half-moved.
    /// </remarks>
    public async Task<ResolvedArmy> SaveAsync(
        Guid userId,
        SaveArmyRequest request,
        CancellationToken cancellationToken)
    {
        var active = request.Active ?? [];
        var reserves = request.Reserves ?? [];
        var tuning = profiles.Tuning;

        if (active.Count > tuning.ActiveLimit)
        {
            throw BattleProblems.ActiveLimit(tuning.ActiveLimit);
        }

        if (reserves.Count > tuning.ReserveLimit)
        {
            throw BattleProblems.ReserveLimit(tuning.ReserveLimit);
        }

        if (active.Count + reserves.Count > tuning.ArmyLimit)
        {
            throw BattleProblems.ArmyLimit(tuning.ArmyLimit);
        }

        // Ownership, once, for everything named. A Unit belonging to somebody else simply is not in
        // this set, and is reported as one that does not exist.
        var owned = await db.PlayerUnits
            .AsNoTracking()
            .Where(unit => unit.OwnerUserId == userId)
            .Select(unit => unit.Id)
            .ToListAsync(cancellationToken);

        var roster = owned.ToHashSet();
        var seen = new HashSet<Guid>();
        var hexes = new HashSet<Hex>();
        var now = clock.GetUtcNow();
        var placements = new List<ArmyPlacement>();

        foreach (var placement in active)
        {
            var hex = new Hex(placement.Column, placement.Row);

            if (!roster.Contains(placement.UnitId))
            {
                throw BattleProblems.UnitNotFound();
            }

            if (!seen.Add(placement.UnitId))
            {
                throw BattleProblems.DuplicateUnit();
            }

            if (!Field.IsDeploymentHexFor(BattleSide.Player, hex))
            {
                throw BattleProblems.HexOutsideHalf(hex.Column, hex.Row, Field.HalfColumns, Field.Rows);
            }

            if (!hexes.Add(hex))
            {
                throw BattleProblems.HexOccupied(hex.Column, hex.Row);
            }

            placements.Add(new ArmyPlacement
            {
                OwnerUserId = userId,
                PlayerUnitId = placement.UnitId,
                Role = ArmyRole.Active,
                HexColumn = hex.Column,
                HexRow = hex.Row,
                UpdatedAt = now,
            });
        }

        // Queue order is the list's order. It is the player's pre-battle decision, and it decides
        // both the order reinforcements are called in and the rear hex each one enters through.
        foreach (var (unitId, queue) in reserves.Select((unitId, queue) => (unitId, queue)))
        {
            if (!roster.Contains(unitId))
            {
                throw BattleProblems.UnitNotFound();
            }

            if (!seen.Add(unitId))
            {
                throw BattleProblems.DuplicateUnit();
            }

            placements.Add(new ArmyPlacement
            {
                OwnerUserId = userId,
                PlayerUnitId = unitId,
                Role = ArmyRole.Reserve,
                ReserveOrder = queue,
                UpdatedAt = now,
            });
        }

        await ReplaceAsync(userId, placements, cancellationToken);

        return await ReadAsync(userId, cancellationToken);
    }

    /// <summary>Clears the account's army and writes the new one, both or neither.</summary>
    private async Task ReplaceAsync(
        Guid userId,
        IReadOnlyList<ArmyPlacement> placements,
        CancellationToken cancellationToken)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);

        try
        {
            await db.ArmyPlacements
                .Where(placement => placement.OwnerUserId == userId)
                .ExecuteDeleteAsync(cancellationToken);

            db.ArmyPlacements.AddRange(placements);
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (Exception refused) when (IsWriteConflict(refused))
        {
            // The hex index, the queue-position index, the primary key and the owner-carrying foreign
            // key are the guarantees this API rests on. When one of them fires it means another
            // request got there first, and the whole transaction rolled back.
            await transaction.RollbackAsync(cancellationToken);

            throw BattleProblems.Conflict();
        }
    }

    private ArmyMember Resolve(UnitPayload unit, ArmyPlacement? placement)
    {
        var definition = units.TryGet(unit.DefinitionKey, out var found)
            ? found
            : throw PreparationProblems.MissingDefinition(unit.DefinitionKey);

        // Weapon content, not the item row: an item records which canonical weapon type it is, and
        // what a Sword is worth in a battle is the creator's to edit.
        var held = unit.Weapons
            .Select(weapon => weapons.Find(weapon.WeaponType))
            .OfType<WeaponDefinition>()
            .ToList();

        return new ArmyMember(
            unit,
            definition,
            profiles.For(definition, held),
            placement?.Role,
            placement is { HexColumn: { } column, HexRow: { } row } ? new Hex(column, row) : null,
            placement?.ReserveOrder);
    }

    /// <summary>Whether the database refused a write because another request reached it first.</summary>
    /// <remarks>
    /// The wrapped case is not hypothetical: the provider's execution strategy reports a failure it
    /// considers transient — a deadlock, most of all — as an <see cref="InvalidOperationException"/>
    /// around the real one.
    /// </remarks>
    private static bool IsWriteConflict(Exception exception)
        => exception is DbUpdateException
            || (exception is InvalidOperationException && exception.InnerException is DbUpdateException);
}
