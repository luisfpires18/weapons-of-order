using Microsoft.EntityFrameworkCore;
using WeaponsOfOrder.Api.Content;
using WeaponsOfOrder.Infrastructure.Gameplay;
using WeaponsOfOrder.Infrastructure.Persistence;

namespace WeaponsOfOrder.Api.Preparation;

/// <summary>
/// Preparation: what a player owns, which Units they have, and what those Units are holding.
/// </summary>
/// <remarks>
/// Every method takes the account id the endpoint read from the session cookie, and every
/// query is filtered by it. The browser names a Unit and an item; it never names an owner, and
/// an identifier belonging to somebody else is answered exactly as one that does not exist.
/// <para>
/// The rules this enforces in code are also enforced by the database, which is what makes them
/// hold under two requests arriving at once. See <see cref="EquippedWeapon"/>.
/// </para>
/// </remarks>
internal sealed class PreparationService(
    WeaponsOfOrderDbContext db,
    UnitCatalogue units,
    WeaponCatalogue weapons,
    TimeProvider clock)
{
    /// <summary>
    /// How many of a player's items the inventory returns, newest first.
    /// </summary>
    /// <remarks>
    /// A bound rather than a feature: nothing in the interface pages, and with one forgeable
    /// weapon nobody reaches it. It exists so an unbounded query is not what a long-running
    /// account eventually discovers.
    /// </remarks>
    public const int InventoryLimit = 50;

    /// <summary>Everything the caller owns, newest first, with where each item currently is.</summary>
    public async Task<IReadOnlyList<InventoryItemPayload>> ListInventoryAsync(
        Guid userId,
        CancellationToken cancellationToken)
    {
        var items = await db.ForgedItems
            .AsNoTracking()
            .Where(item => item.OwnerUserId == userId)
            .OrderByDescending(item => item.ForgedAt)
            .ThenByDescending(item => item.Id)
            .Take(InventoryLimit)
            .ToListAsync(cancellationToken);

        var equipped = await EquippedByItemAsync(userId, cancellationToken);
        var owned = await OwnedUnitsAsync(userId, cancellationToken);

        return [.. items.Select(item => ToPayload(item, equipped.GetValueOrDefault(item.Id), owned))];
    }

    /// <summary>
    /// The caller's Units, resolved through the creator's content, with their loadouts.
    /// </summary>
    /// <remarks>
    /// Also where the temporary starter roster is granted, because recruitment does not exist
    /// and a first read is the earliest honest moment to notice an account has no Units.
    /// </remarks>
    public async Task<IReadOnlyList<UnitPayload>> ListUnitsAsync(Guid userId, CancellationToken cancellationToken)
    {
        await EnsureStarterUnitsAsync(userId, cancellationToken);

        var owned = await OwnedUnitsAsync(userId, cancellationToken);
        var equipped = await db.EquippedWeapons
            .AsNoTracking()
            .Where(weapon => weapon.OwnerUserId == userId)
            .ToListAsync(cancellationToken);

        var items = await ItemsByIdAsync(equipped.Select(weapon => weapon.ItemId), cancellationToken);

        return [.. owned.Select(unit => ToPayload(unit, equipped, items))];
    }

    /// <summary>Puts one of the caller's own weapons into one of their own Unit's hands.</summary>
    public async Task<UnitPayload> EquipAsync(
        Guid userId,
        Guid unitId,
        Guid itemId,
        int? slot,
        CancellationToken cancellationToken)
    {
        var unit = await RequireUnitAsync(userId, unitId, cancellationToken);
        var item = await db.ForgedItems
            .AsNoTracking()
            .FirstOrDefaultAsync(
                candidate => candidate.Id == itemId && candidate.OwnerUserId == userId,
                cancellationToken)
            ?? throw PreparationProblems.ItemNotFound();

        var weapon = weapons.Find(item.WeaponType)
            ?? throw PreparationProblems.ItemNotEquippable(item.WeaponType);

        // Not scoped by owner: the item is already known to be the caller's, and an item
        // equipped anywhere at all is unavailable. The primary key on the equipment row is
        // what actually guarantees that; this is the readable rejection in front of it.
        if (await db.EquippedWeapons.AnyAsync(held => held.ItemId == itemId, cancellationToken))
        {
            throw PreparationProblems.ItemAlreadyEquipped();
        }

        var held = await db.EquippedWeapons
            .AsNoTracking()
            .Where(weapon => weapon.PlayerUnitId == unitId)
            .ToListAsync(cancellationToken);

        var firstTaken = held.Any(weapon => weapon.OccupiesFirstSlot);
        var secondTaken = held.Any(weapon => weapon.OccupiesSecondSlot);
        var (first, second) = Place(weapon, slot, firstTaken, secondTaken);

        db.EquippedWeapons.Add(new EquippedWeapon
        {
            ItemId = item.Id,
            PlayerUnitId = unit.Id,
            OwnerUserId = userId,
            OccupiesFirstSlot = first,
            OccupiesSecondSlot = second,
            EquippedAt = clock.GetUtcNow(),
        });

        await SaveAsync(cancellationToken);

        return await ReadUnitAsync(unit, cancellationToken);
    }

    /// <summary>Takes a weapon out of a Unit's hands and returns it to the inventory.</summary>
    public async Task<UnitPayload> UnequipAsync(
        Guid userId,
        Guid unitId,
        Guid itemId,
        CancellationToken cancellationToken)
    {
        var unit = await RequireUnitAsync(userId, unitId, cancellationToken);

        var equipped = await db.EquippedWeapons.FirstOrDefaultAsync(
            weapon => weapon.ItemId == itemId
                && weapon.PlayerUnitId == unitId
                && weapon.OwnerUserId == userId,
            cancellationToken)
            ?? throw PreparationProblems.ItemNotEquipped();

        db.EquippedWeapons.Remove(equipped);
        await SaveAsync(cancellationToken);

        return await ReadUnitAsync(unit, cancellationToken);
    }

    /// <summary>
    /// Which hands a weapon goes into.
    /// </summary>
    /// <remarks>
    /// A 2-slot weapon occupies the whole loadout, so it is never assigned to a hand — asking
    /// for one is rejected rather than quietly reinterpreted. A 1-slot weapon goes where the
    /// player said, or into the first free hand when they did not say.
    /// </remarks>
    private static (bool First, bool Second) Place(
        WeaponDefinition weapon,
        int? slot,
        bool firstTaken,
        bool secondTaken)
    {
        if (slot is { } requested && requested is < Loadout.FirstSlot or > Loadout.SecondSlot)
        {
            throw PreparationProblems.UnknownSlot(Loadout.WeaponSlots);
        }

        if (weapon.SlotCost >= Loadout.WeaponSlots)
        {
            if (slot is not null)
            {
                throw PreparationProblems.NeedsBothHands();
            }

            return firstTaken || secondTaken
                ? throw PreparationProblems.SlotOccupied()
                : (true, true);
        }

        var target = slot ?? (firstTaken ? Loadout.SecondSlot : Loadout.FirstSlot);

        if (target == Loadout.FirstSlot ? firstTaken : secondTaken)
        {
            throw PreparationProblems.SlotOccupied();
        }

        return (target == Loadout.FirstSlot, target == Loadout.SecondSlot);
    }

    /// <summary>
    /// Grants the temporary starter roster: one Unit for each definition marked as a starter.
    /// </summary>
    /// <remarks>
    /// Lazy, like the forge's opening materials, and for the same reason — there is no
    /// recruitment yet, so where a first Unit comes from is a placeholder. Idempotency is the
    /// unique index on owner + starter grant key rather than the read below, which is only
    /// what keeps the ordinary case from attempting a write at all.
    /// <para>
    /// Adding a fourth starter definition later grants it once to every existing account with
    /// no schema change, because the grant is keyed per definition rather than per roster.
    /// </para>
    /// </remarks>
    private async Task EnsureStarterUnitsAsync(Guid userId, CancellationToken cancellationToken)
    {
        var starters = units.Starters;
        if (starters.Count == 0)
        {
            return;
        }

        var granted = await db.PlayerUnits
            .AsNoTracking()
            .Where(unit => unit.OwnerUserId == userId && unit.StarterGrantKey != null)
            .Select(unit => unit.StarterGrantKey!)
            .ToListAsync(cancellationToken);

        var missing = starters
            .Where(definition => !granted.Contains(definition.Key, StringComparer.Ordinal))
            .ToList();

        if (missing.Count == 0)
        {
            return;
        }

        var now = clock.GetUtcNow();

        // One grant per transaction, rather than the whole roster in one write. Two first
        // loads arriving together would otherwise each hold index entries the other is
        // waiting for, and the database would break the tie by failing one of them with a
        // deadlock. A single-row transaction holds one lock and cannot deadlock: the loser
        // simply finds the grant already taken.
        foreach (var definition in missing)
        {
            db.PlayerUnits.Add(new PlayerUnit
            {
                Id = Guid.CreateVersion7(),
                OwnerUserId = userId,
                DefinitionKey = definition.Key,
                StarterGrantKey = definition.Key,
                Origin = PlayerUnitOrigin.StarterGrant,
                AcquiredAt = now,
            });

            try
            {
                await db.SaveChangesAsync(cancellationToken);
            }
            catch (Exception refused) when (IsWriteConflict(refused))
            {
                // Another request granted this one first. Drop the attempt and carry on: the
                // player did nothing wrong and the caller reads the winner below.
                foreach (var entry in db.ChangeTracker.Entries<PlayerUnit>().ToList())
                {
                    entry.State = EntityState.Detached;
                }
            }
        }
    }

    private async Task<PlayerUnit> RequireUnitAsync(
        Guid userId,
        Guid unitId,
        CancellationToken cancellationToken)
        => await db.PlayerUnits
            .AsNoTracking()
            .FirstOrDefaultAsync(unit => unit.Id == unitId && unit.OwnerUserId == userId, cancellationToken)
            ?? throw PreparationProblems.UnitNotFound();

    /// <summary>
    /// The caller's units, in the order the creator authored their definitions.
    /// </summary>
    /// <remarks>
    /// Content order rather than row order: a starter roster is granted in one write, so every
    /// unit in it shares an acquisition time and there is nothing in the rows to break the tie
    /// with. Acquisition time and identity still order duplicates of one definition against
    /// each other.
    /// </remarks>
    private async Task<List<PlayerUnit>> OwnedUnitsAsync(Guid userId, CancellationToken cancellationToken)
    {
        var owned = await db.PlayerUnits
            .AsNoTracking()
            .Where(unit => unit.OwnerUserId == userId)
            .ToListAsync(cancellationToken);

        return
        [
            .. owned
                .OrderBy(unit => units.PositionOf(unit.DefinitionKey))
                .ThenBy(unit => unit.AcquiredAt)
                .ThenBy(unit => unit.Id),
        ];
    }

    private async Task<Dictionary<Guid, EquippedWeapon>> EquippedByItemAsync(
        Guid userId,
        CancellationToken cancellationToken)
        => (await db.EquippedWeapons
                .AsNoTracking()
                .Where(weapon => weapon.OwnerUserId == userId)
                .ToListAsync(cancellationToken))
            .ToDictionary(weapon => weapon.ItemId);

    private async Task<Dictionary<Guid, ForgedItem>> ItemsByIdAsync(
        IEnumerable<Guid> itemIds,
        CancellationToken cancellationToken)
    {
        var ids = itemIds.ToArray();

        return ids.Length == 0
            ? []
            : (await db.ForgedItems
                    .AsNoTracking()
                    .Where(item => ids.Contains(item.Id))
                    .ToListAsync(cancellationToken))
                .ToDictionary(item => item.Id);
    }

    /// <summary>One Unit as it stands after a change to its loadout.</summary>
    private async Task<UnitPayload> ReadUnitAsync(PlayerUnit unit, CancellationToken cancellationToken)
    {
        var equipped = await db.EquippedWeapons
            .AsNoTracking()
            .Where(weapon => weapon.PlayerUnitId == unit.Id)
            .ToListAsync(cancellationToken);

        var items = await ItemsByIdAsync(equipped.Select(weapon => weapon.ItemId), cancellationToken);

        return ToPayload(unit, equipped, items);
    }

    private UnitPayload ToPayload(
        PlayerUnit unit,
        IReadOnlyList<EquippedWeapon> equipped,
        IReadOnlyDictionary<Guid, ForgedItem> items)
    {
        var definition = Definition(unit.DefinitionKey);

        var weapons = equipped
            .Where(weapon => weapon.PlayerUnitId == unit.Id)
            .OrderBy(weapon => weapon.OccupiesFirstSlot ? Loadout.FirstSlot : Loadout.SecondSlot)
            .Select(weapon => items.TryGetValue(weapon.ItemId, out var item)
                ? new UnitWeaponPayload(
                    item.Id,
                    Name(item),
                    item.WeaponType,
                    Lowercase(item.Craftsmanship),
                    [.. weapon.Slots])
                : null)
            .OfType<UnitWeaponPayload>()
            .ToList();

        return new UnitPayload(
            unit.Id,
            definition.Key,
            definition.DisplayName,
            Lowercase(definition.Type),
            definition.Kingdom,
            definition.Tier,
            Lowercase(definition.MaxArmor),
            definition.Mounted,
            Loadout.WeaponSlots,
            weapons);
    }

    private InventoryItemPayload ToPayload(
        ForgedItem item,
        EquippedWeapon? equipped,
        IReadOnlyList<PlayerUnit> owned)
    {
        var weapon = weapons.Find(item.WeaponType);

        var holder = equipped is null
            ? null
            : owned.FirstOrDefault(unit => unit.Id == equipped.PlayerUnitId);

        return new InventoryItemPayload(
            item.Id,
            Name(item),
            item.WeaponType,
            Lowercase(item.Craftsmanship),
            Lowercase(item.Origin),
            item.ForgedAt,
            weapon?.SlotCost,
            weapon is not null,
            holder is null || equipped is null
                ? null
                : new EquippedOnPayload(holder.Id, Definition(holder.DefinitionKey).DisplayName, [.. equipped.Slots]));
    }

    private UnitDefinition Definition(string key)
        => units.TryGet(key, out var definition)
            ? definition
            : throw PreparationProblems.MissingDefinition(key);

    /// <summary>
    /// What to call an item. Weapon content is the authority; the canonical type is the
    /// fallback, so an item whose metadata has not been authored still reads as itself.
    /// </summary>
    private string Name(ForgedItem item) => weapons.Find(item.WeaponType)?.DisplayName ?? item.WeaponType;

    private async Task SaveAsync(CancellationToken cancellationToken)
    {
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (Exception refused) when (IsWriteConflict(refused))
        {
            // The item primary key, the two per-slot unique indexes and the owner-carrying
            // foreign keys are the guarantees this API rests on. When one of them fires it
            // means another request got there first, and the whole transaction rolled back.
            throw PreparationProblems.Conflict();
        }
    }

    /// <summary>
    /// Whether the database refused a write because another request reached it first.
    /// </summary>
    /// <remarks>
    /// The wrapped case is not hypothetical: the provider's execution strategy reports a
    /// failure it considers transient — a deadlock, most of all — as an
    /// <see cref="InvalidOperationException"/> around the real one. Catching only
    /// <see cref="DbUpdateException"/> lets those through as an unhandled fault, which is the
    /// wrong answer for something the player did nothing to cause.
    /// </remarks>
    private static bool IsWriteConflict(Exception exception)
        => exception is DbUpdateException
            || (exception is InvalidOperationException && exception.InnerException is DbUpdateException);

    /// <summary>
    /// Enum names, lower-cased, so the wire contract is a stable string the client can switch
    /// on rather than an ordinal that moves when the enum is edited.
    /// </summary>
    private static string Lowercase<T>(T value)
        where T : struct, Enum
        => value.ToString().ToLowerInvariant();
}
