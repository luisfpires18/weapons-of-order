namespace WeaponsOfOrder.Api.Tests;

/// <summary>
/// The API with a driveable clock, for the army and battle tests.
/// </summary>
/// <remarks>
/// It reads the real <c>server/content</c> and the real <c>appsettings.json</c>, so the battle
/// these tests fight is the one the creator configured rather than a fixture that could drift
/// from it.
/// </remarks>
public class BattleApiFactory : PreparationApiFactory;

/// <summary>
/// Unit and weapon combat values pinned to round numbers.
/// </summary>
/// <remarks>
/// The arithmetic of a loadout deserves an exact assertion, and asserting exactly against the
/// creator's own balance values would mean a balance edit breaks a test about addition. These
/// overrides make the numbers the test's rather than the content's.
/// </remarks>
public sealed class KnownStatsApiFactory : PreparationApiFactory
{
    public const int UnitHp = 300;
    public const int UnitPower = 10;
    public const int UnitDefense = 12;
    public const double UnitAttackInterval = 2.0;
    public const double UnitCriticalChance = 0.1;

    public const int WeaponPower = 7;
    public const double WeaponCriticalChance = 0.25;
    public const int WeaponRange = 4;

    /// <summary>What the weapon's weight costs in Attack Interval seconds.</summary>
    public const double WeaponIntervalCost = 0.5;

    protected override IEnumerable<KeyValuePair<string, string?>> AdditionalConfiguration =>
    [
        new("UnitContent:Units:0:Combat:Hp", $"{UnitHp}"),
        new("UnitContent:Units:0:Combat:Power", $"{UnitPower}"),
        new("UnitContent:Units:0:Combat:Defense", $"{UnitDefense}"),
        new("UnitContent:Units:0:Combat:AttackIntervalSeconds", $"{UnitAttackInterval}"),
        new("UnitContent:Units:0:Combat:CriticalChance", $"{UnitCriticalChance}"),

        new("WeaponContent:Weapons:0:Power", $"{WeaponPower}"),
        new("WeaponContent:Weapons:0:CriticalChance", $"{WeaponCriticalChance}"),
        new("WeaponContent:Weapons:0:Weight", "Heavy"),
        new("WeaponContent:Weapons:0:Range", $"{WeaponRange}"),

        new("Combat:WeightIntervalSeconds:Heavy", $"{WeaponIntervalCost}"),
    ];
}

/// <summary>
/// Deployment limits low enough that three starter Units can exceed them, plus a fourth Unit.
/// </summary>
/// <remarks>
/// The real limits are 8 active and 8 in reserve, and an account currently starts with three
/// Units — so testing what happens at the limit means lowering it rather than granting five more
/// Units nobody authored. The fourth definition exists only inside this factory.
/// <para>
/// The training opposition still has to fit inside the lowered limits, which is why the active
/// limit here is three rather than one.
/// </para>
/// </remarks>
public sealed class TightArmyLimitsApiFactory : PreparationApiFactory
{
    public const int ActiveLimit = 3;
    public const int ReserveLimit = 1;
    public const int ArmyLimit = 4;
    public const string FourthUnitKey = "test.fourth";

    protected override IEnumerable<KeyValuePair<string, string?>> AdditionalConfiguration =>
    [
        new("Combat:Tuning:ActiveLimit", $"{ActiveLimit}"),
        new("Combat:Tuning:ReserveLimit", $"{ReserveLimit}"),
        new("Combat:Tuning:ArmyLimit", $"{ArmyLimit}"),

        new("UnitContent:Units:3:Key", FourthUnitKey),
        new("UnitContent:Units:3:DisplayName", "Fourth"),
        new("UnitContent:Units:3:Type", "Regular"),
        new("UnitContent:Units:3:Kingdom", "Arkazia"),
        new("UnitContent:Units:3:Tier", "1"),
        new("UnitContent:Units:3:MaxArmor", "Heavy"),
        new("UnitContent:Units:3:Mounted", "false"),
        new("UnitContent:Units:3:Starter", "true"),
        new("UnitContent:Units:3:Combat:Hp", "200"),
        new("UnitContent:Units:3:Combat:Power", "8"),
        new("UnitContent:Units:3:Combat:Defense", "5"),
        new("UnitContent:Units:3:Combat:AttackIntervalSeconds", "1.5"),
        new("UnitContent:Units:3:Combat:CriticalChance", "0.05"),
    ];
}

/// <summary>
/// A battle whose guards expire almost immediately.
/// </summary>
/// <remarks>
/// Proves the API returns a guard Draw as an ordinary result rather than as a failure, without a
/// test having to wait for a two-minute battle to be simulated.
/// </remarks>
public sealed class ShortGuardApiFactory : PreparationApiFactory
{
    public const double MaximumDurationSeconds = 1;

    protected override IEnumerable<KeyValuePair<string, string?>> AdditionalConfiguration =>
    [
        new("Combat:Tuning:MaximumDurationSeconds", $"{MaximumDurationSeconds}"),
        new("Combat:Tuning:NoProgressSeconds", $"{MaximumDurationSeconds}"),
    ];
}

/// <summary>A tick of zero, to prove a battle tuned into a stopped clock never starts.</summary>
public sealed class StoppedClockApiFactory : WeaponsOfOrderApiFactory
{
    protected override IEnumerable<KeyValuePair<string, string?>> AdditionalConfiguration =>
    [
        new("Combat:Tuning:TickMilliseconds", "0"),
    ];
}

/// <summary>A Mounted Unit configured to be slower than one on foot, which canon does not allow.</summary>
public sealed class SlowMountApiFactory : WeaponsOfOrderApiFactory
{
    protected override IEnumerable<KeyValuePair<string, string?>> AdditionalConfiguration =>
    [
        new("Combat:Tuning:MountedMovementSecondsPerHex", "9"),
    ];
}

/// <summary>Training opposition standing in the player's own half.</summary>
public sealed class MisplacedOpponentApiFactory : WeaponsOfOrderApiFactory
{
    protected override IEnumerable<KeyValuePair<string, string?>> AdditionalConfiguration =>
    [
        new("Combat:TrainingOpponent:Active:0:Column", "1"),
    ];
}

/// <summary>
/// A Unit definition with its whole Combat block removed.
/// </summary>
/// <remarks>
/// Configuration cannot delete a key, so the block is emptied field by field. What matters is
/// that a Unit content file written without combat stats stops the application rather than
/// producing a Unit that cannot be fielded and nobody finds out until a battle.
/// </remarks>
public sealed class UnitWithoutCombatStatsApiFactory : WeaponsOfOrderApiFactory
{
    protected override IEnumerable<KeyValuePair<string, string?>> AdditionalConfiguration =>
    [
        new("UnitContent:Units:4:Key", "test.statless"),
        new("UnitContent:Units:4:DisplayName", "Statless"),
        new("UnitContent:Units:4:Type", "Regular"),
        new("UnitContent:Units:4:Kingdom", "Arkazia"),
        new("UnitContent:Units:4:Tier", "1"),
        new("UnitContent:Units:4:MaxArmor", "Heavy"),
        new("UnitContent:Units:4:Mounted", "false"),
    ];
}

/// <summary>A Unit that would start every battle already dead.</summary>
public sealed class ZeroHpUnitApiFactory : WeaponsOfOrderApiFactory
{
    protected override IEnumerable<KeyValuePair<string, string?>> AdditionalConfiguration =>
    [
        new("UnitContent:Units:0:Combat:Hp", "0"),
    ];
}

/// <summary>A Critical Chance greater than certainty.</summary>
public sealed class ImpossibleCriticalChanceApiFactory : WeaponsOfOrderApiFactory
{
    protected override IEnumerable<KeyValuePair<string, string?>> AdditionalConfiguration =>
    [
        new("UnitContent:Units:1:Combat:CriticalChance", "1.5"),
    ];
}

/// <summary>A weapon that reaches nowhere.</summary>
public sealed class UnreachableWeaponApiFactory : WeaponsOfOrderApiFactory
{
    protected override IEnumerable<KeyValuePair<string, string?>> AdditionalConfiguration =>
    [
        new("WeaponContent:Weapons:0:Range", "0"),
    ];
}

/// <summary>A weight the registry does not name.</summary>
public sealed class UnknownWeaponWeightApiFactory : WeaponsOfOrderApiFactory
{
    protected override IEnumerable<KeyValuePair<string, string?>> AdditionalConfiguration =>
    [
        new("WeaponContent:Weapons:0:Weight", "Ponderous"),
    ];
}
