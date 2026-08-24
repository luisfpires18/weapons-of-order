using System.Reflection;
using Xunit;

namespace WeaponsOfOrder.Combat.Tests;

/// <summary>
/// What the simulator is allowed to know.
/// </summary>
/// <remarks>
/// The other tests here prove the rules. These prove the boundary the rules live behind, which is
/// the thing that erodes quietly: one flag at a time, each individually reasonable, until the
/// "pure" simulator is reading Unit content and the seam has to be rebuilt.
/// <para>
/// A combatant is a bag of final numbers. If a value cannot be expressed as one of these, it does
/// not belong on this side of the boundary.
/// </para>
/// </remarks>
public class CombatBoundaryTests
{
    /// <summary>Canon's six universal stats, plus the Range the weapon registry authors.</summary>
    [Fact]
    public void A_combatant_is_described_by_exactly_the_canonical_stats()
    {
        var stats = typeof(CombatantStats)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Select(property => property.Name)
            .OrderBy(name => name, StringComparer.Ordinal);

        Assert.Equal(
            [
                "AttackIntervalSeconds",
                "CriticalChance",
                "Defense",
                "Hp",
                "MovementSpeed",
                "Power",
                "Range",
            ],
            stats);
    }

    /// <summary>
    /// Movement Speed is the stat; Mounted is not, anywhere on the boundary.
    /// </summary>
    /// <remarks>
    /// Mounted is Unit identity and belongs to content. The API resolves it into a number before
    /// building a battle, so the simulator has nothing to interpret — and no reason to ever learn
    /// what a kingdom, a class or a piece of equipment is either.
    /// </remarks>
    [Fact]
    public void Nothing_on_the_boundary_mentions_Mounted()
    {
        var named = typeof(BattleInput).Assembly
            .GetExportedTypes()
            .SelectMany(type => type
                .GetMembers(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly)
                .Select(member => $"{type.Name}.{member.Name}"))
            .Where(name => name.Contains("Mounted", StringComparison.OrdinalIgnoreCase))
            .ToList();

        Assert.Empty(named);
    }

    /// <summary>Movement Speed is a scalar, so standard movement is a number a test can state.</summary>
    [Fact]
    public void Standard_movement_is_a_speed_of_one()
    {
        var tuning = CombatTuning.Default;

        Assert.Equal(
            tuning.MovementIntervalMilliseconds(1.0),
            (int)Math.Round(tuning.BaseMovementSecondsPerHex * 1000, MidpointRounding.AwayFromZero));
    }
}
