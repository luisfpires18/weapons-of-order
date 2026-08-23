namespace WeaponsOfOrder.Api.Preparation;

/// <summary>Which Unit is holding an item, for the inventory's benefit.</summary>
internal sealed record EquippedOnPayload(Guid UnitId, string UnitName, IReadOnlyList<int> Slots);

/// <summary>
/// One item the player owns.
/// </summary>
/// <remarks>
/// Everything here either comes from the item's own record or from weapon content. There is
/// no sale value, item level, gear score, durability or rarity separate from craftsmanship:
/// none of those exist in this game, and inventing one to fill a column would be inventing a
/// system.
/// <para>
/// <paramref name="SlotCost"/> is null when no weapon content authors this type, which is also
/// what <paramref name="Equippable"/> reports. The item is still owned and still listed; it
/// simply cannot be put in a hand until its metadata is authored.
/// </para>
/// </remarks>
internal sealed record InventoryItemPayload(
    Guid Id,
    string Name,
    string WeaponType,
    string Craftsmanship,
    string Origin,
    DateTimeOffset ForgedAt,
    int? SlotCost,
    bool Equippable,
    EquippedOnPayload? EquippedOn);

/// <summary>A weapon in a Unit's hands, and which of the two hands it fills.</summary>
internal sealed record UnitWeaponPayload(
    Guid ItemId,
    string Name,
    string WeaponType,
    string Craftsmanship,
    IReadOnlyList<int> Slots);

/// <summary>
/// One of the player's Units, resolved through the creator's Unit content.
/// </summary>
/// <remarks>
/// There is no class or specialisation field. Canon derives the current class from
/// <c>Unit + loadout</c>, the creator has not authored the names or the mappings, and a
/// placeholder would be a lie the interface then has to display. The loadout is published;
/// the resolver that turns it into a specialisation attaches later without this shape moving.
/// </remarks>
internal sealed record UnitPayload(
    Guid Id,
    string DefinitionKey,
    string Name,
    string Type,
    string Kingdom,
    int Tier,
    string MaxArmor,
    bool Mounted,
    int WeaponSlots,
    IReadOnlyList<UnitWeaponPayload> Weapons);

/// <summary>
/// The item to equip, and optionally which hand to put it in.
/// </summary>
/// <remarks>
/// No owner and no Unit: the owner comes from the session cookie and the Unit from the route.
/// <paramref name="Slot"/> absent means the first free hand, which is what makes equipping a
/// second sword one press rather than a choice the player has to make twice.
/// </remarks>
internal sealed record EquipRequest(Guid ItemId, int? Slot);

internal sealed record UnequipRequest(Guid ItemId);
