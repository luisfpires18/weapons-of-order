namespace WeaponsOfOrder.Api.Tests;

/// <summary>
/// The API with a driveable clock, for tests that forge the weapons they then equip.
/// </summary>
/// <remarks>
/// It reads the real <c>server/content</c> files, so the definitions these tests assert on are
/// the ones the creator actually authored rather than fixtures that could drift from them.
/// </remarks>
public class PreparationApiFactory : ForgeApiFactory;

/// <summary>
/// One display name changed and nothing else, to prove the Unit catalogue is content.
/// </summary>
/// <remarks>
/// The same PostgreSQL database as every other factory here, so a unit granted under the
/// authored name can be read back under this one. That is the whole claim: the persistent row
/// is an instance of a definition, not a copy of it, and renaming needs no migration.
/// </remarks>
public sealed class RenamedUnitApiFactory : PreparationApiFactory
{
    public const string NewDisplayName = "Vanguard";

    protected override IEnumerable<KeyValuePair<string, string?>> AdditionalConfiguration =>
    [
        new("UnitContent:Units:0:DisplayName", NewDisplayName),
    ];
}

/// <summary>
/// Adds a synthetic two-slot weapon type to the weapon content.
/// </summary>
/// <remarks>
/// Deliberately not a Bow. Bows are canonically two-slot, but no bow content exists and adding
/// one to make a test convenient would put unauthored content in the game. This type exists
/// only inside this factory, and the item that carries it is a forged sword whose recorded
/// weapon type the test rewrites — which is enough to prove the loadout shape holds for a
/// weapon that fills both hands without the item being written down twice.
/// </remarks>
public sealed class TwoSlotWeaponApiFactory : PreparationApiFactory
{
    public const string WeaponType = "TestTwoSlotWeapon";

    protected override IEnumerable<KeyValuePair<string, string?>> AdditionalConfiguration =>
    [
        new("WeaponContent:Weapons:1:Type", WeaponType),
        new("WeaponContent:Weapons:1:DisplayName", "Test Two-Slot Weapon"),
        new("WeaponContent:Weapons:1:SlotCost", "2"),
    ];
}

/// <summary>Two definitions sharing one key.</summary>
public sealed class DuplicateUnitKeyApiFactory : WeaponsOfOrderApiFactory
{
    protected override IEnumerable<KeyValuePair<string, string?>> AdditionalConfiguration =>
    [
        new("UnitContent:Units:1:Key", "arkazia.melee"),
    ];
}

public sealed class UnnamedUnitApiFactory : WeaponsOfOrderApiFactory
{
    protected override IEnumerable<KeyValuePair<string, string?>> AdditionalConfiguration =>
    [
        new("UnitContent:Units:0:DisplayName", string.Empty),
    ];
}

public sealed class UnknownUnitTypeApiFactory : WeaponsOfOrderApiFactory
{
    protected override IEnumerable<KeyValuePair<string, string?>> AdditionalConfiguration =>
    [
        new("UnitContent:Units:0:Type", "Champion"),
    ];
}

public sealed class OutOfRangeTierApiFactory : WeaponsOfOrderApiFactory
{
    protected override IEnumerable<KeyValuePair<string, string?>> AdditionalConfiguration =>
    [
        new("UnitContent:Units:0:Tier", "4"),
    ];
}

public sealed class UnknownArmorClassApiFactory : WeaponsOfOrderApiFactory
{
    protected override IEnumerable<KeyValuePair<string, string?>> AdditionalConfiguration =>
    [
        new("UnitContent:Units:0:MaxArmor", "Plate"),
    ];
}

public sealed class UnknownKingdomApiFactory : WeaponsOfOrderApiFactory
{
    protected override IEnumerable<KeyValuePair<string, string?>> AdditionalConfiguration =>
    [
        new("UnitContent:Units:0:Kingdom", "Somewhere"),
    ];
}

/// <summary>A Mounted value that is not a boolean at all.</summary>
public sealed class UnreadableMountedApiFactory : WeaponsOfOrderApiFactory
{
    protected override IEnumerable<KeyValuePair<string, string?>> AdditionalConfiguration =>
    [
        new("UnitContent:Units:0:Mounted", "sometimes"),
    ];
}

/// <summary>A slot cost outside the two slots a unit has.</summary>
public sealed class ImpossibleSlotCostApiFactory : WeaponsOfOrderApiFactory
{
    protected override IEnumerable<KeyValuePair<string, string?>> AdditionalConfiguration =>
    [
        new("WeaponContent:Weapons:0:SlotCost", "3"),
    ];
}
