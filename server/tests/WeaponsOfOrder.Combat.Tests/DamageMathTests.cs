using Xunit;

namespace WeaponsOfOrder.Combat.Tests;

/// <summary>
/// The damage pipeline, asserted against the numbers the combat canon writes down.
/// </summary>
public class DamageMathTests
{
    private static readonly CombatTuning Tuning = CombatTuning.Default;

    [Fact]
    public void One_Power_is_five_raw_auto_damage()
    {
        Assert.Equal(5d, DamageMath.RawAuto(1, Tuning));
        Assert.Equal(50d, DamageMath.RawAuto(10, Tuning));
    }

    /// <summary>The examples the canon lists for the mitigation curve, to three decimal places.</summary>
    [Theory]
    [InlineData(0, 0.0)]
    [InlineData(3, 0.029)]
    [InlineData(10, 0.091)]
    [InlineData(25, 0.200)]
    [InlineData(50, 0.333)]
    [InlineData(100, 0.500)]
    public void Defense_reduces_damage_along_the_canonical_curve(int defense, double expected)
        => Assert.Equal(expected, DamageMath.Reduction(defense, Tuning), 3);

    [Fact]
    public void A_normal_hit_is_Power_times_five_before_Defense()
        => Assert.Equal(50, DamageMath.Damage(10, AttackKind.Normal, critical: false, defense: 0, Tuning));

    [Fact]
    public void A_critical_doubles_the_attack()
        => Assert.Equal(100, DamageMath.Damage(10, AttackKind.Normal, critical: true, defense: 0, Tuning));

    /// <summary>The canon's worked example: a 50 hit becomes a 125 Heavy and a 250 Heavy critical.</summary>
    [Fact]
    public void A_Heavy_attack_is_two_and_a_half_times_an_auto()
    {
        Assert.Equal(125, DamageMath.Damage(10, AttackKind.Heavy, critical: false, defense: 0, Tuning));
        Assert.Equal(250, DamageMath.Damage(10, AttackKind.Heavy, critical: true, defense: 0, Tuning));
    }

    [Fact]
    public void Defense_is_applied_after_the_coefficient_and_the_critical()
    {
        // 10 Power -> 50 raw -> 100 critical -> halved by 100 Defense.
        Assert.Equal(50, DamageMath.Damage(10, AttackKind.Normal, critical: true, defense: 100, Tuning));

        // 10 Power -> 50 raw -> 125 Heavy -> 20% off at 25 Defense.
        Assert.Equal(100, DamageMath.Damage(10, AttackKind.Heavy, critical: false, defense: 25, Tuning));
    }

    /// <summary>
    /// Halves round up, not to the nearest even number.
    /// </summary>
    /// <remarks>
    /// 10 Power against 300 Defense is exactly 12.5. .NET's default rounding is banker's, which
    /// would answer 12 here and 14 for 13.5 — not what "round to the nearest whole number" means
    /// to anyone reading the canon, and a difference a player would eventually notice.
    /// </remarks>
    [Fact]
    public void A_damage_of_exactly_one_half_rounds_up()
        => Assert.Equal(13, DamageMath.Damage(10, AttackKind.Normal, critical: false, defense: 300, Tuning));

    [Fact]
    public void A_hit_that_lands_always_takes_at_least_one_HP()
    {
        Assert.Equal(1, DamageMath.Damage(0, AttackKind.Normal, critical: false, defense: 0, Tuning));
        Assert.Equal(1, DamageMath.Damage(1, AttackKind.Normal, critical: false, defense: 100_000, Tuning));
    }

    [Fact]
    public void Energy_starts_at_nothing_and_gains_ten_per_auto()
    {
        Assert.Equal(10, DamageMath.EnergyAfterAttack(0, AttackKind.Normal, Tuning));
        Assert.Equal(50, DamageMath.EnergyAfterAttack(40, AttackKind.Normal, Tuning));
    }

    [Fact]
    public void Energy_does_not_overflow_past_the_bar()
        => Assert.Equal(100, DamageMath.EnergyAfterAttack(95, AttackKind.Normal, Tuning));

    [Fact]
    public void A_full_bar_spends_itself_on_the_Heavy_attack()
    {
        Assert.Equal(AttackKind.Heavy, DamageMath.KindFor(100, Tuning));
        Assert.Equal(0, DamageMath.EnergyAfterAttack(100, AttackKind.Heavy, Tuning));
    }

    [Fact]
    public void A_bar_short_of_full_is_still_an_ordinary_auto()
        => Assert.Equal(AttackKind.Normal, DamageMath.KindFor(90, Tuning));
}
