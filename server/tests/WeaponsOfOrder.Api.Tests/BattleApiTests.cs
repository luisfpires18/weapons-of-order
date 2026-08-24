using System.Net;
using WeaponsOfOrder.Api.Battle;
using Xunit;

namespace WeaponsOfOrder.Api.Tests;

/// <summary>
/// Fighting a battle: what the server decides, and what the browser is not allowed to.
/// </summary>
public class BattleApiTests(BattleApiFactory factory)
    : IClassFixture<BattleApiFactory>, IAsyncLifetime
{
    private CancellationToken Token => TestContext.Current.CancellationToken;

    public ValueTask InitializeAsync() => factory.InitializeAsync();

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    [Fact]
    public async Task An_army_with_nobody_deployed_cannot_start_a_battle()
    {
        using var client = await factory.SignedInAsync("battle-empty", Token);

        await client.ReadArmyAsync(Token);

        var refused = await client.PostSimulateAsync(Token);

        Assert.Equal(HttpStatusCode.Conflict, refused.StatusCode);
        Assert.Equal(BattleProblems.EmptyArmyCode, await refused.ProblemCodeAsync(Token));
    }

    /// <summary>
    /// A deployed army fights, and the log that comes back is enough to draw the battle from.
    /// </summary>
    [Fact]
    public async Task A_deployed_army_resolves_a_whole_battle()
    {
        using var client = await factory.SignedInAsync("battle-resolve", Token);

        var army = await client.ReadArmyAsync(Token);
        await client.DeployEveryUnitAsync(army, Token);

        var result = await client.SimulateAsync(Token);

        Assert.Contains(result.Outcome, new[] { "playervictory", "opponentvictory", "draw" });
        Assert.Contains(
            result.Reason,
            new[] { "elimination", "mutualelimination", "maximumduration", "noprogress" });

        // Every deployed Unit is on the board at time zero, and the opposition is there with them.
        var deployments = result.Events("deployed");
        Assert.Equal(army.Units.Count + 3, deployments.Count);
        Assert.All(deployments, moment => Assert.Equal(0, moment.Number("time")));

        // The player's combatants carry their own Unit identifiers back; the training opposition is
        // not made of Units and carries none.
        var mine = result.Combatants.Where(combatant => combatant.Side == "player").ToList();
        Assert.Equal(army.Units.Count, mine.Count);
        Assert.All(mine, combatant => Assert.NotNull(combatant.UnitId));
        Assert.All(
            result.Combatants.Where(combatant => combatant.Side == "opponent"),
            combatant => Assert.Null(combatant.UnitId));

        // A battle happened rather than a board being drawn.
        Assert.NotEmpty(result.Events("attack"));
        Assert.NotEmpty(result.Events("moved"));

        var ended = Assert.Single(result.Events("ended"));
        Assert.Equal(result.DurationMilliseconds, ended.Number("time"));
        Assert.Equal(result.Outcome, ended.Text("outcome"));
    }

    /// <summary>
    /// The attack log tells the client what happened, so it never has to work it out.
    /// </summary>
    [Fact]
    public async Task An_attack_event_carries_its_own_result()
    {
        using var client = await factory.SignedInAsync("battle-attack", Token);

        await client.DeployEveryUnitAsync(await client.ReadArmyAsync(Token), Token);

        var result = await client.SimulateAsync(Token);
        var attacks = result.Events("attack");

        Assert.NotEmpty(attacks);
        Assert.All(attacks, attack =>
        {
            Assert.NotEqual(attack.Text("attackerId"), attack.Text("targetId"));
            Assert.Contains(attack.Text("attack"), new[] { "normal", "heavy" });
            Assert.True(attack.Number("damage") >= 1);
            Assert.InRange(attack.Number("attackerEnergy"), 0, 100);
            Assert.True(attack.Number("targetHp") >= 0);
        });

        // Energy climbs to the top of the bar and is spent there. This is the client seeing the
        // Heavy attack rather than inferring it from a damage number.
        Assert.All(
            attacks.Where(attack => attack.Text("attack") == "heavy"),
            attack => Assert.Equal(0, attack.Number("attackerEnergy")));
    }

    /// <summary>
    /// The browser can say which Units go where. It cannot say anything else.
    /// </summary>
    /// <remarks>
    /// The request below names stats, a winner and a seed. All of it is ignored: the response's
    /// stats are the ones the server resolved from its own content, and the battle is fought with
    /// a seed the server minted.
    /// </remarks>
    [Fact]
    public async Task The_browser_cannot_name_its_own_stats_or_its_own_result()
    {
        using var client = await factory.SignedInAsync("battle-spoof", Token);

        var army = await client.ReadArmyAsync(Token);
        var honest = army.Unit(PreparationApi.MeleeKey);

        var saved = await client.SaveArmyAsync(
            new
            {
                active = new[]
                {
                    new
                    {
                        unitId = honest.UnitId,
                        column = 3,
                        row = 3,
                        hp = 999_999,
                        power = 999,
                        defense = 999,
                        range = 8,
                        stats = new { hp = 999_999, power = 999 },
                    },
                },
            },
            Token);

        var deployed = saved.Unit(PreparationApi.MeleeKey);
        Assert.Equal(honest.Stats, deployed.Stats);
        Assert.NotEqual(999_999, deployed.Stats.Hp);

        var response = await client.PostAsync(
            BattleApi.SimulatePath,
            new { outcome = "playervictory", seed = "1", opponent = Array.Empty<object>() },
            Token);

        Assert.True(response.IsSuccessStatusCode);

        var result = await client.SimulateAsync(Token);

        // The opposition turned up regardless of the caller asking for none of it.
        Assert.Equal(3, result.Combatants.Count(combatant => combatant.Side == "opponent"
            && combatant.ReserveOrder is null));
        Assert.NotEqual("1", result.Seed);

        var fighting = Assert.Single(
            result.Combatants,
            combatant => combatant.Side == "player" && combatant.UnitId == honest.UnitId);
        Assert.Equal(honest.Stats.Hp, fighting.Stats.Hp);
        Assert.Equal(honest.Stats.Power, fighting.Stats.Power);
    }

    /// <summary>The seed is the server's, and a fresh one is minted for every battle.</summary>
    [Fact]
    public async Task Each_battle_gets_its_own_server_minted_seed()
    {
        using var client = await factory.SignedInAsync("battle-seed", Token);

        await client.DeployEveryUnitAsync(await client.ReadArmyAsync(Token), Token);

        var seeds = new List<string>();

        for (var battle = 0; battle < 4; battle++)
        {
            seeds.Add((await client.SimulateAsync(Token)).Seed);
        }

        Assert.Equal(seeds.Count, seeds.Distinct().Count());
    }

    /// <summary>The reserve enters through the hex the deployment screen said it would.</summary>
    [Fact]
    public async Task A_reserve_enters_through_its_published_entry_hex()
    {
        using var client = await factory.SignedInAsync("battle-reserve", Token);

        var army = await client.ReadArmyAsync(Token);

        var saved = await client.SaveArmyAsync(
            new
            {
                active = new[]
                {
                    new { unitId = army.Unit(PreparationApi.MeleeKey).UnitId, column = 3, row = 3 },
                    new { unitId = army.Unit(PreparationApi.RangedKey).UnitId, column = 2, row = 3 },
                },
                reserves = new[] { army.Unit(PreparationApi.MountedKey).UnitId },
            },
            Token);

        var waiting = saved.Unit(PreparationApi.MountedKey);
        Assert.NotNull(waiting.ReserveEntryHex);

        var result = await client.SimulateAsync(Token);
        var reserve = Assert.Single(result.Combatants, combatant => combatant.UnitId == waiting.UnitId);

        Assert.Equal(0, reserve.ReserveOrder);
        Assert.Equal(waiting.ReserveEntryHex, reserve.ReserveEntryHex);

        // It either entered at that hex or it never entered. There is no third hex it could use.
        foreach (var entry in result.Events("reserve").Where(moment => moment.Text("id") == reserve.Id))
        {
            var hex = entry.GetProperty("hex");
            Assert.Equal(reserve.ReserveEntryHex!.Column, hex.GetProperty("column").GetInt32());
            Assert.Equal(reserve.ReserveEntryHex.Row, hex.GetProperty("row").GetInt32());
        }
    }
}

/// <summary>A battle whose guards expire almost at once still comes back as an ordinary result.</summary>
public class GuardedBattleTests(ShortGuardApiFactory factory)
    : IClassFixture<ShortGuardApiFactory>, IAsyncLifetime
{
    private CancellationToken Token => TestContext.Current.CancellationToken;

    public ValueTask InitializeAsync() => factory.InitializeAsync();

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    [Fact]
    public async Task A_guard_expiry_is_a_Draw_and_not_a_failure()
    {
        using var client = await factory.SignedInAsync("battle-guard", Token);

        await client.DeployEveryUnitAsync(await client.ReadArmyAsync(Token), Token);

        var result = await client.SimulateAsync(Token);

        Assert.Equal("draw", result.Outcome);
        Assert.Contains(result.Reason, new[] { "maximumduration", "noprogress" });
        Assert.True(result.DurationMilliseconds <= ShortGuardApiFactory.MaximumDurationSeconds * 1000);

        // The guard is a stopwatch, not a scythe: the survivors are recorded alive.
        Assert.Contains(result.Combatants, combatant => combatant.EndState == "active");
    }
}
