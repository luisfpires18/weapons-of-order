using Xunit;

namespace WeaponsOfOrder.Combat.Tests;

/// <summary>
/// Battles the simulator refuses to resolve.
/// </summary>
/// <remarks>
/// These are caller bugs rather than player mistakes — the API checks a deployment against the
/// same limits before it gets here — so the simulator throws instead of inventing an outcome.
/// </remarks>
public class BattleInputValidationTests
{
    private static ArmyUnderTest Ordinary(string name, Hex hex)
        => new ArmyUnderTest(name).Deploy("unit", hex, Fight.Stats());

    private static InvalidBattleInputException Refused(BattleInput input)
        => Assert.Throws<InvalidBattleInputException>(() => BattleSimulator.Simulate(input));

    [Fact]
    public void An_army_with_no_Units_cannot_fight()
    {
        var refusal = Refused(Fight.Between(new ArmyUnderTest("player"), Ordinary("opponent", new Hex(4, 3))));

        Assert.Contains("no Units", refusal.Message);
    }

    [Fact]
    public void A_Unit_deployed_in_the_wrong_half_is_refused()
    {
        // (4,3) belongs to the opponent's half.
        var refusal = Refused(Fight.Between(Ordinary("player", new Hex(4, 3)), Ordinary("opponent", new Hex(5, 3))));

        Assert.Contains("deployment half", refusal.Message);
    }

    [Fact]
    public void Two_Units_on_one_hex_are_refused()
    {
        var player = new ArmyUnderTest("player")
            .Deploy("one", new Hex(3, 3), Fight.Stats())
            .Deploy("two", new Hex(3, 3), Fight.Stats());

        Assert.Contains("deployed on", Refused(Fight.Between(player, Ordinary("opponent", new Hex(4, 3)))).Message);
    }

    [Fact]
    public void More_active_Units_than_the_deployment_limit_are_refused()
    {
        var player = new ArmyUnderTest("player");

        for (var row = 0; row < 7; row++)
        {
            player.Deploy($"a{row}", new Hex(3, row), Fight.Stats());
            player.Deploy($"b{row}", new Hex(2, row), Fight.Stats());
        }

        var refusal = Refused(Fight.Between(player, Ordinary("opponent", new Hex(4, 3))));

        Assert.Contains("active limit is 8", refusal.Message);
    }

    [Fact]
    public void More_reserves_than_the_reserve_limit_are_refused()
    {
        var player = new ArmyUnderTest("player").Deploy("front", new Hex(3, 3), Fight.Stats());

        for (var index = 0; index < 9; index++)
        {
            player.Reserve($"r{index}", Fight.Stats());
        }

        var refusal = Refused(Fight.Between(player, Ordinary("opponent", new Hex(4, 3))));

        Assert.Contains("reserve limit is 8", refusal.Message);
    }

    [Fact]
    public void More_Units_than_the_army_limit_are_refused()
    {
        // Limits that do not add up: eight on the board and eight behind them is sixteen, and this
        // army is only allowed ten. Each individual limit is satisfied and the total is not.
        var tuning = CombatTuning.Default with { ArmyLimit = 10 };
        var player = new ArmyUnderTest("player");

        for (var index = 0; index < 8; index++)
        {
            player.Deploy($"a{index}", new Hex(3, index % 7), Fight.Stats());
        }

        // Eight on the board plus three behind them, against an army limit of ten.
        for (var index = 0; index < 3; index++)
        {
            player.Reserve($"r{index}", Fight.Stats());
        }

        var refusal = Refused(Fight.Between(player, Ordinary("opponent", new Hex(4, 3)), tuning));

        Assert.Contains("army limit is 10", refusal.Message);
    }

    [Fact]
    public void A_Unit_that_starts_with_no_HP_is_refused()
    {
        var player = new ArmyUnderTest("player").Deploy("ghost", new Hex(3, 3), Fight.Stats(hp: 0));

        Assert.Contains("no HP", Refused(Fight.Between(player, Ordinary("opponent", new Hex(4, 3)))).Message);
    }

    [Fact]
    public void A_Unit_with_no_reach_is_refused()
    {
        var player = new ArmyUnderTest("player").Deploy("unarmed", new Hex(3, 3), Fight.Stats(range: 0));

        Assert.Contains("no attack range", Refused(Fight.Between(player, Ordinary("opponent", new Hex(4, 3)))).Message);
    }

    [Fact]
    public void A_Unit_with_no_Attack_Interval_is_refused()
    {
        var player = new ArmyUnderTest("player").Deploy("blur", new Hex(3, 3), Fight.Stats(interval: 0));

        Assert.Contains("broken clock", Refused(Fight.Between(player, Ordinary("opponent", new Hex(4, 3)))).Message);
    }
}
