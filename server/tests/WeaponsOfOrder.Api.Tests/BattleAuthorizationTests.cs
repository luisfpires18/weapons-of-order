using System.Net;
using System.Net.Http.Json;
using WeaponsOfOrder.Api.Battle;
using Xunit;

namespace WeaponsOfOrder.Api.Tests;

/// <summary>
/// Who may deploy an army, and whose Units they may deploy.
/// </summary>
/// <remarks>
/// The battle API's whole security position is that the browser names Units and hexes and nothing
/// else. These are the tests that hold it to that.
/// </remarks>
public class BattleAuthorizationTests(BattleApiFactory factory)
    : IClassFixture<BattleApiFactory>, IAsyncLifetime
{
    private CancellationToken Token => TestContext.Current.CancellationToken;

    public ValueTask InitializeAsync() => factory.InitializeAsync();

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    [Fact]
    public async Task Reading_an_army_without_a_session_is_refused()
    {
        using var client = factory.CreateAuthClient();

        var response = await client.Http.GetAsync(BattleApi.ArmyPath, Token);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Deploying_without_a_session_is_refused()
    {
        using var client = factory.CreateAuthClient();

        var response = await client.PostArmyAsync(new { active = Array.Empty<object>() }, Token);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Starting_a_battle_without_a_session_is_refused()
    {
        using var client = factory.CreateAuthClient();

        var response = await client.PostSimulateAsync(Token);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    /// <summary>
    /// A session cookie alone is not enough for a mutation.
    /// </summary>
    /// <remarks>
    /// Cookie authentication means a cross-site page can make the browser send the session cookie.
    /// It cannot read a same-origin JSON response, which is where the antiforgery token comes from.
    /// </remarks>
    [Theory]
    [InlineData(BattleApi.ArmyPath)]
    [InlineData(BattleApi.SimulatePath)]
    public async Task A_mutation_without_an_antiforgery_token_is_refused(string path)
    {
        using var client = await factory.SignedInAsync($"battle-csrf-{path.GetHashCode(StringComparison.Ordinal)}", Token);

        var response = await client.PostAsync(path, new { }, csrfToken: null, Token);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var problem = await response.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>(Token);
        Assert.Equal("antiforgery", problem.GetProperty("code").GetString());
    }

    [Fact]
    public async Task A_wrong_antiforgery_token_is_refused()
    {
        using var client = await factory.SignedInAsync("battle-csrf-wrong", Token);

        var response = await client.PostAsync(BattleApi.ArmyPath, new { }, "not-a-token", Token);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    /// <summary>
    /// Another account's Unit is reported exactly as one that does not exist.
    /// </summary>
    /// <remarks>
    /// Not "forbidden": telling a caller that an identifier is real but not theirs answers a
    /// question they had no business asking, and turns a guess into information.
    /// </remarks>
    [Fact]
    public async Task Another_accounts_Unit_cannot_be_deployed()
    {
        using var mine = await factory.SignedInAsync("battle-owner-a", Token);
        using var theirs = await factory.SignedInAsync("battle-owner-b", Token);

        var stranger = (await theirs.ReadArmyAsync(Token)).Unit(PreparationApi.MeleeKey);

        var refused = await mine.PostArmyAsync(
            new { active = new[] { new { unitId = stranger.UnitId, column = 1, row = 1 } } },
            Token);

        Assert.Equal(HttpStatusCode.NotFound, refused.StatusCode);
        Assert.Equal(BattleProblems.UnitNotFoundCode, await refused.ProblemCodeAsync(Token));

        // And nothing was written for either account.
        Assert.All((await mine.ReadArmyAsync(Token)).Units, unit => Assert.Equal("unplaced", unit.Role));
        Assert.All((await theirs.ReadArmyAsync(Token)).Units, unit => Assert.Equal("unplaced", unit.Role));
    }

    [Fact]
    public async Task Another_accounts_Unit_cannot_be_held_in_reserve()
    {
        using var mine = await factory.SignedInAsync("battle-owner-c", Token);
        using var theirs = await factory.SignedInAsync("battle-owner-d", Token);

        var stranger = (await theirs.ReadArmyAsync(Token)).Unit(PreparationApi.MeleeKey);

        var refused = await mine.PostArmyAsync(
            new { active = Array.Empty<object>(), reserves = new[] { stranger.UnitId } },
            Token);

        Assert.Equal(HttpStatusCode.NotFound, refused.StatusCode);
        Assert.Equal(BattleProblems.UnitNotFoundCode, await refused.ProblemCodeAsync(Token));
    }

    [Fact]
    public async Task A_Unit_that_does_not_exist_at_all_is_refused()
    {
        using var client = await factory.SignedInAsync("battle-owner-e", Token);

        var refused = await client.PostArmyAsync(
            new { active = new[] { new { unitId = Guid.CreateVersion7(), column = 1, row = 1 } } },
            Token);

        Assert.Equal(HttpStatusCode.NotFound, refused.StatusCode);
    }

    /// <summary>One account's deployment is invisible to another, and unaffected by it.</summary>
    [Fact]
    public async Task Two_accounts_keep_their_own_armies()
    {
        using var mine = await factory.SignedInAsync("battle-isolate-a", Token);
        using var theirs = await factory.SignedInAsync("battle-isolate-b", Token);

        var myUnit = (await mine.ReadArmyAsync(Token)).Unit(PreparationApi.MeleeKey);
        var theirUnit = (await theirs.ReadArmyAsync(Token)).Unit(PreparationApi.MeleeKey);

        // The same hex, in each of their own halves. Uniqueness is per account, not per board.
        await mine.SaveArmyAsync(
            new { active = new[] { new { unitId = myUnit.UnitId, column = 2, row = 2 } } },
            Token);

        await theirs.SaveArmyAsync(
            new { active = new[] { new { unitId = theirUnit.UnitId, column = 2, row = 2 } } },
            Token);

        Assert.Equal(new HexView(2, 2), (await mine.ReadArmyAsync(Token)).Unit(PreparationApi.MeleeKey).Hex);
        Assert.Equal(new HexView(2, 2), (await theirs.ReadArmyAsync(Token)).Unit(PreparationApi.MeleeKey).Hex);

        var mineIds = (await mine.ReadArmyAsync(Token)).Units.Select(unit => unit.UnitId).ToHashSet();
        var theirIds = (await theirs.ReadArmyAsync(Token)).Units.Select(unit => unit.UnitId).ToHashSet();

        Assert.Empty(mineIds.Intersect(theirIds));
    }
}

/// <summary>The deployment limits, at the point where they bite.</summary>
public class ArmyLimitTests(TightArmyLimitsApiFactory factory)
    : IClassFixture<TightArmyLimitsApiFactory>, IAsyncLifetime
{
    private CancellationToken Token => TestContext.Current.CancellationToken;

    public ValueTask InitializeAsync() => factory.InitializeAsync();

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    [Fact]
    public async Task More_Units_than_the_deployment_limit_are_refused()
    {
        using var client = await factory.SignedInAsync("limit-active", Token);

        var army = await client.ReadArmyAsync(Token);
        Assert.Equal(TightArmyLimitsApiFactory.ActiveLimit, army.Limits.Active);
        Assert.True(army.Units.Count > army.Limits.Active);

        var refused = await client.PostArmyAsync(
            new
            {
                active = army.Units
                    .Select((unit, index) => new { unitId = unit.UnitId, column = 3, row = index })
                    .ToArray(),
            },
            Token);

        Assert.Equal(HttpStatusCode.BadRequest, refused.StatusCode);
        Assert.Equal(BattleProblems.ActiveLimitCode, await refused.ProblemCodeAsync(Token));

        // The limit is a boundary, not an obstacle: exactly the limit is accepted.
        var accepted = await client.SaveArmyAsync(
            new
            {
                active = army.Units
                    .Take(army.Limits.Active)
                    .Select((unit, index) => new { unitId = unit.UnitId, column = 3, row = index })
                    .ToArray(),
            },
            Token);

        Assert.Equal(army.Limits.Active, accepted.Units.Count(unit => unit.Role == "active"));
    }

    [Fact]
    public async Task More_reserves_than_the_reserve_limit_are_refused()
    {
        using var client = await factory.SignedInAsync("limit-reserve", Token);

        var army = await client.ReadArmyAsync(Token);
        Assert.Equal(TightArmyLimitsApiFactory.ReserveLimit, army.Limits.Reserve);

        var refused = await client.PostArmyAsync(
            new
            {
                active = Array.Empty<object>(),
                reserves = army.Units.Take(army.Limits.Reserve + 1).Select(unit => unit.UnitId).ToArray(),
            },
            Token);

        Assert.Equal(HttpStatusCode.BadRequest, refused.StatusCode);
        Assert.Equal(BattleProblems.ReserveLimitCode, await refused.ProblemCodeAsync(Token));
    }
}
