using Microsoft.Extensions.Options;
using Xunit;

namespace WeaponsOfOrder.Api.Tests;

/// <summary>
/// Combat configuration and content the application refuses to start on.
/// </summary>
/// <remarks>
/// These values are meant to be edited, which is exactly why they are checked at startup. A
/// battle tuned into a state it cannot run in should stop the application with the offending
/// setting named, rather than surfacing as a battle that never comes back.
/// </remarks>
public sealed class CombatConfigurationTests
{
    [Fact]
    public void A_stopped_combat_clock_is_rejected()
        => AssertRefusesToStart<StoppedClockApiFactory>("TickMilliseconds");

    [Fact]
    public void A_Mounted_Unit_slower_than_one_on_foot_is_rejected()
        => AssertRefusesToStart<SlowMountApiFactory>("MountedMovementSecondsPerHex");

    [Fact]
    public void Training_opposition_standing_in_the_player_half_is_rejected()
        => AssertRefusesToStart<MisplacedOpponentApiFactory>("deployment half");

    [Fact]
    public void A_Unit_with_no_combat_stats_is_rejected()
        => AssertRefusesToStart<UnitWithoutCombatStatsApiFactory>("needs a Combat block");

    [Fact]
    public void A_Unit_that_starts_with_no_HP_is_rejected()
        => AssertRefusesToStart<ZeroHpUnitApiFactory>("Combat.Hp");

    [Fact]
    public void A_Critical_Chance_above_certainty_is_rejected()
        => AssertRefusesToStart<ImpossibleCriticalChanceApiFactory>("CriticalChance");

    [Fact]
    public void A_weapon_with_no_reach_is_rejected()
        => AssertRefusesToStart<UnreachableWeaponApiFactory>("Range 0");

    [Fact]
    public void An_unknown_weapon_weight_is_rejected()
        => AssertRefusesToStart<UnknownWeaponWeightApiFactory>("Weight 'Ponderous'");

    private static void AssertRefusesToStart<TFactory>(string expectedInMessage)
        where TFactory : WeaponsOfOrderApiFactory, new()
    {
        using var factory = new TFactory();

        var exception = Assert.Throws<OptionsValidationException>(() => _ = factory.Services);

        Assert.Contains(expectedInMessage, exception.Message, StringComparison.Ordinal);
    }
}
