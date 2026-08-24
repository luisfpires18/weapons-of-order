using System.Net;
using WeaponsOfOrder.Api.Battle;
using Xunit;

namespace WeaponsOfOrder.Api.Tests;

/// <summary>
/// Deploying an army: what the server accepts, what it refuses, and what survives a sign-out.
/// </summary>
public class ArmyDeploymentTests(BattleApiFactory factory)
    : IClassFixture<BattleApiFactory>, IAsyncLifetime
{
    private CancellationToken Token => TestContext.Current.CancellationToken;

    public ValueTask InitializeAsync() => factory.InitializeAsync();

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    [Fact]
    public async Task An_account_starts_with_its_Units_unplaced()
    {
        using var client = await factory.SignedInAsync("army-empty", Token);

        var army = await client.ReadArmyAsync(Token);

        Assert.NotEmpty(army.Units);
        Assert.All(army.Units, unit => Assert.Equal("unplaced", unit.Role));
        Assert.False(army.Ready);

        // The board the client draws is the server's, not a constant the browser carries.
        Assert.Equal(8, army.Battlefield.Columns);
        Assert.Equal(7, army.Battlefield.Rows);
        Assert.Equal(4, army.Battlefield.DeploymentColumns);
        Assert.Equal(8, army.Limits.Active);
        Assert.Equal(8, army.Limits.Reserve);
        Assert.Equal(16, army.Limits.Army);
    }

    [Fact]
    public async Task A_Unit_can_be_placed_repositioned_and_removed()
    {
        using var client = await factory.SignedInAsync("army-place", Token);

        var army = await client.ReadArmyAsync(Token);
        var unit = army.Unit(PreparationApi.MeleeKey);

        var placed = await client.SaveArmyAsync(
            new { active = new[] { new { unitId = unit.UnitId, column = 2, row = 3 } } },
            Token);

        var deployed = placed.Unit(PreparationApi.MeleeKey);
        Assert.Equal("active", deployed.Role);
        Assert.Equal(new HexView(2, 3), deployed.Hex);
        Assert.True(placed.Ready);

        var moved = await client.SaveArmyAsync(
            new { active = new[] { new { unitId = unit.UnitId, column = 0, row = 6 } } },
            Token);

        Assert.Equal(new HexView(0, 6), moved.Unit(PreparationApi.MeleeKey).Hex);

        // An empty army is a legitimate thing to save: it is how a deployment is cleared.
        var cleared = await client.SaveArmyAsync(new { active = Array.Empty<object>() }, Token);

        Assert.All(cleared.Units, entry => Assert.Equal("unplaced", entry.Role));
        Assert.False(cleared.Ready);
    }

    /// <summary>Queue order is the player's, and it decides where each reserve enters.</summary>
    [Fact]
    public async Task Reserves_keep_their_order_and_are_told_where_they_will_enter()
    {
        using var client = await factory.SignedInAsync("army-reserves", Token);

        var army = await client.ReadArmyAsync(Token);
        var melee = army.Unit(PreparationApi.MeleeKey);
        var ranged = army.Unit(PreparationApi.RangedKey);
        var mounted = army.Unit(PreparationApi.MountedKey);

        var saved = await client.SaveArmyAsync(
            new
            {
                active = new[] { new { unitId = melee.UnitId, column = 3, row = 3 } },
                reserves = new[] { mounted.UnitId, ranged.UnitId },
            },
            Token);

        Assert.Equal(0, saved.Unit(PreparationApi.MountedKey).ReserveOrder);
        Assert.Equal(1, saved.Unit(PreparationApi.RangedKey).ReserveOrder);

        // The rear column of the player's own half, a row each. A reserve enters here or it waits.
        Assert.Equal(new HexView(0, 0), saved.Unit(PreparationApi.MountedKey).ReserveEntryHex);
        Assert.Equal(new HexView(0, 1), saved.Unit(PreparationApi.RangedKey).ReserveEntryHex);

        var reordered = await client.SaveArmyAsync(
            new
            {
                active = new[] { new { unitId = melee.UnitId, column = 3, row = 3 } },
                reserves = new[] { ranged.UnitId, mounted.UnitId },
            },
            Token);

        Assert.Equal(0, reordered.Unit(PreparationApi.RangedKey).ReserveOrder);
        Assert.Equal(1, reordered.Unit(PreparationApi.MountedKey).ReserveOrder);
    }

    [Fact]
    public async Task A_Unit_cannot_be_placed_twice()
    {
        using var client = await factory.SignedInAsync("army-duplicate", Token);

        var unit = (await client.ReadArmyAsync(Token)).Unit(PreparationApi.MeleeKey);

        var refused = await client.PostArmyAsync(
            new
            {
                active = new[]
                {
                    new { unitId = unit.UnitId, column = 1, row = 1 },
                    new { unitId = unit.UnitId, column = 2, row = 2 },
                },
            },
            Token);

        Assert.Equal(HttpStatusCode.Conflict, refused.StatusCode);
        Assert.Equal(BattleProblems.DuplicateUnitCode, await refused.ProblemCodeAsync(Token));
    }

    [Fact]
    public async Task A_Unit_cannot_be_deployed_and_held_in_reserve_at_once()
    {
        using var client = await factory.SignedInAsync("army-both", Token);

        var unit = (await client.ReadArmyAsync(Token)).Unit(PreparationApi.MeleeKey);

        var refused = await client.PostArmyAsync(
            new
            {
                active = new[] { new { unitId = unit.UnitId, column = 1, row = 1 } },
                reserves = new[] { unit.UnitId },
            },
            Token);

        Assert.Equal(BattleProblems.DuplicateUnitCode, await refused.ProblemCodeAsync(Token));
    }

    [Fact]
    public async Task Two_Units_cannot_stand_on_one_hex()
    {
        using var client = await factory.SignedInAsync("army-hex", Token);

        var army = await client.ReadArmyAsync(Token);

        var refused = await client.PostArmyAsync(
            new
            {
                active = new[]
                {
                    new { unitId = army.Unit(PreparationApi.MeleeKey).UnitId, column = 2, row = 2 },
                    new { unitId = army.Unit(PreparationApi.RangedKey).UnitId, column = 2, row = 2 },
                },
            },
            Token);

        Assert.Equal(HttpStatusCode.Conflict, refused.StatusCode);
        Assert.Equal(BattleProblems.HexOccupiedCode, await refused.ProblemCodeAsync(Token));
    }

    /// <summary>
    /// The deployment half is a boundary, not a suggestion.
    /// </summary>
    /// <remarks>
    /// Column 4 is the opponent's front line and column 8 is off the board entirely. Both are
    /// refused before anything is written, and the database would refuse them again.
    /// </remarks>
    [Theory]
    [InlineData(4, 0)]
    [InlineData(7, 3)]
    [InlineData(8, 0)]
    [InlineData(-1, 0)]
    [InlineData(0, 7)]
    [InlineData(0, -1)]
    public async Task A_Unit_cannot_be_deployed_outside_its_own_half(int column, int row)
    {
        using var client = await factory.SignedInAsync($"army-half-{column}-{row}", Token);

        var unit = (await client.ReadArmyAsync(Token)).Unit(PreparationApi.MeleeKey);

        var refused = await client.PostArmyAsync(
            new { active = new[] { new { unitId = unit.UnitId, column, row } } },
            Token);

        Assert.Equal(HttpStatusCode.BadRequest, refused.StatusCode);
        Assert.Equal(BattleProblems.HexOutsideHalfCode, await refused.ProblemCodeAsync(Token));
    }

    /// <summary>A refused save leaves the army exactly as it was.</summary>
    [Fact]
    public async Task A_refused_deployment_changes_nothing()
    {
        using var client = await factory.SignedInAsync("army-atomic", Token);

        var army = await client.ReadArmyAsync(Token);
        var melee = army.Unit(PreparationApi.MeleeKey);
        var ranged = army.Unit(PreparationApi.RangedKey);

        await client.SaveArmyAsync(
            new { active = new[] { new { unitId = melee.UnitId, column = 3, row = 3 } } },
            Token);

        var refused = await client.PostArmyAsync(
            new
            {
                active = new[]
                {
                    new { unitId = ranged.UnitId, column = 1, row = 1 },
                    new { unitId = ranged.UnitId, column = 2, row = 2 },
                },
            },
            Token);

        Assert.False(refused.IsSuccessStatusCode);

        var after = await client.ReadArmyAsync(Token);
        Assert.Equal(new HexView(3, 3), after.Unit(PreparationApi.MeleeKey).Hex);
        Assert.Equal("unplaced", after.Unit(PreparationApi.RangedKey).Role);
    }

    /// <summary>
    /// The deployment is the server's, and it is still there after signing out and back in.
    /// </summary>
    [Fact]
    public async Task A_deployment_survives_a_fresh_sign_in()
    {
        var email = TestAccounts.NewEmail("army-persist");

        using (var first = factory.CreateAuthClient())
        {
            await first.SignInAsNewAccountAsync(factory, email, TestAccounts.ValidPassword, Token);

            var army = await first.ReadArmyAsync(Token);

            await first.SaveArmyAsync(
                new
                {
                    active = new[]
                    {
                        new { unitId = army.Unit(PreparationApi.MeleeKey).UnitId, column = 3, row = 2 },
                    },
                    reserves = new[] { army.Unit(PreparationApi.MountedKey).UnitId },
                },
                Token);
        }

        // A second browser holding the same account, which is what a reload or another device is.
        using var second = factory.CreateAuthClient();
        await second.SignInAsync(email, TestAccounts.ValidPassword, Token);

        var reloaded = await second.ReadArmyAsync(Token);

        Assert.Equal(new HexView(3, 2), reloaded.Unit(PreparationApi.MeleeKey).Hex);
        Assert.Equal(0, reloaded.Unit(PreparationApi.MountedKey).ReserveOrder);
        Assert.Equal("unplaced", reloaded.Unit(PreparationApi.RangedKey).Role);
    }
}
