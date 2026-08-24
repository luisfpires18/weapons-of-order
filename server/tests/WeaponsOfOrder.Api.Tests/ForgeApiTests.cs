using System.Net;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using WeaponsOfOrder.Infrastructure.Gameplay;
using WeaponsOfOrder.Infrastructure.Persistence;
using Xunit;

namespace WeaponsOfOrder.Api.Tests;

/// <summary>
/// The ordinary forge end to end: choose, pay, heat, strike, keep what you made.
/// </summary>
/// <remarks>
/// Every timing here is exact because the clock is under test control. The temperatures in
/// the comments are what the configured rates produce — 30 a second heating, 18 cooling, 22
/// lost to each blow — so a change to the balance values shows up as a failed assertion
/// rather than as a quietly different game.
/// </remarks>
public sealed class ForgeApiTests(ForgeApiFactory factory) : IClassFixture<ForgeApiFactory>
{
    private CancellationToken Cancellation => TestContext.Current.CancellationToken;

    [Fact]
    public async Task A_signed_in_player_sees_their_stock_and_the_recipe_before_anything_is_forged()
    {
        using var client = await SignedInAsync("forge-state");

        var state = await client.ReadStateAsync(Cancellation);

        Assert.Equal(new MaterialsView(24, 12, 8), state.Materials);
        Assert.Null(state.Session);

        var sword = Assert.Single(state.Recipes);
        Assert.Equal(ForgeApi.SwordRecipe, sword.Key);
        Assert.Equal("Sword", sword.Name);
        Assert.Equal("Sword", sword.WeaponType);
        Assert.Equal(new MaterialsView(3, 1, 0), sword.Cost);
        Assert.True(sword.Affordable);
    }

    [Theory]
    [InlineData("GET", "/api/forge/state")]
    [InlineData("GET", "/api/forge/items")]
    [InlineData("POST", "/api/forge/begin")]
    [InlineData("POST", "/api/forge/heat")]
    [InlineData("POST", "/api/forge/strike")]
    [InlineData("POST", "/api/forge/abandon")]
    public async Task Every_forge_route_refuses_a_caller_without_a_session(string method, string path)
    {
        using var client = factory.CreateAuthClient();

        var response = method == "GET"
            ? await client.Http.GetAsync(path, Cancellation)
            : await client.PostAsync(path, new { recipeKey = ForgeApi.SwordRecipe, heating = true }, Cancellation);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("unauthenticated", await TestAccounts.ReadProblemCodeAsync(response, Cancellation));
    }

    [Fact]
    public async Task Beginning_a_forge_charges_the_recipe_once_and_puts_a_cold_workpiece_on_the_anvil()
    {
        using var client = await SignedInAsync("forge-begin");

        var state = await client.BeginAsync(Cancellation);

        Assert.Equal(new MaterialsView(21, 11, 8), state.Materials);

        var session = state.RequireSession();
        Assert.Equal("active", session.Status);
        Assert.Equal("cold", session.Band);
        Assert.Equal(0, session.Temperature);
        Assert.False(session.Heating);
        Assert.Equal(0, session.StrikesTaken);
        Assert.Equal(3, session.StrikesRequired);
        Assert.Empty(session.Strikes);
        Assert.Null(session.Craftsmanship);
        Assert.Null(session.ItemId);

        // Re-reading must not charge again, and must not produce a second workpiece.
        var reread = await client.ReadStateAsync(Cancellation);
        Assert.Equal(new MaterialsView(21, 11, 8), reread.Materials);
        Assert.Equal(session.Id, reread.RequireSession().Id);
    }

    [Fact]
    public async Task A_second_begin_is_refused_and_costs_nothing()
    {
        using var client = await SignedInAsync("forge-double-begin");

        await client.BeginAsync(Cancellation);
        var second = await client.PostBeginAsync(Cancellation);

        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
        Assert.Equal("forge_in_progress", await TestAccounts.ReadProblemCodeAsync(second, Cancellation));

        var state = await client.ReadStateAsync(Cancellation);
        Assert.Equal(new MaterialsView(21, 11, 8), state.Materials);
        Assert.Equal(1, await CountSessionsAsync(client));
    }

    /// <summary>
    /// Two begins in flight at once. The unique index on the active session is what settles
    /// it, and because the deduction and the insert are one write, the loser is charged
    /// nothing.
    /// </summary>
    [Fact]
    public async Task Two_simultaneous_begins_produce_one_workpiece_and_one_deduction()
    {
        using var client = await SignedInAsync("forge-race");

        // Reading first so the opening stock is already granted: the race under test is the
        // one over starting a forge, not the one over being given materials.
        await client.ReadStateAsync(Cancellation);

        var attempts = await Task.WhenAll(
            client.PostBeginAsync(Cancellation),
            client.PostBeginAsync(Cancellation));

        Assert.Equal(1, attempts.Count(response => response.IsSuccessStatusCode));
        Assert.All(
            attempts.Where(response => !response.IsSuccessStatusCode),
            response => Assert.Equal(HttpStatusCode.Conflict, response.StatusCode));

        var state = await client.ReadStateAsync(Cancellation);
        Assert.Equal(new MaterialsView(21, 11, 8), state.Materials);
        Assert.Equal(1, await CountSessionsAsync(client));
    }

    [Fact]
    public async Task An_unknown_recipe_is_refused()
    {
        using var client = await SignedInAsync("forge-unknown-recipe");

        var response = await client.PostBeginAsync(Cancellation, "weapon.not-a-thing");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("forge_unknown_recipe", await TestAccounts.ReadProblemCodeAsync(response, Cancellation));
        Assert.Equal(0, await CountSessionsAsync(client));
    }

    [Fact]
    public async Task Heating_and_striking_are_only_possible_with_a_workpiece_on_the_anvil()
    {
        using var client = await SignedInAsync("forge-not-active");

        var heat = await client.PostHeatAsync(true, Cancellation);
        var strike = await client.PostStrikeAsync(Cancellation);

        Assert.Equal("forge_not_active", await TestAccounts.ReadProblemCodeAsync(heat, Cancellation));
        Assert.Equal("forge_not_active", await TestAccounts.ReadProblemCodeAsync(strike, Cancellation));
    }

    /// <summary>
    /// Three well-timed blows. The player never states a temperature or a quality; they say
    /// "heating" and "strike", and the server's own clock decides the rest.
    /// </summary>
    [Fact]
    public async Task Ideal_timing_produces_an_epic_sword_and_one_owned_item()
    {
        using var client = await SignedInAsync("forge-epic");

        await client.BeginAsync(Cancellation);
        await client.HeatAsync(true, Cancellation);

        var first = await StrikeAfterAsync(client, 2.4);      // 0 -> 72
        var second = await StrikeAfterAsync(client, 0.8);     // 50 -> 74
        var third = await StrikeAfterAsync(client, 0.8);      // 52 -> 76

        Assert.Equal("ideal", first.Strikes[0].Band);
        Assert.Equal("ideal", second.Strikes[1].Band);
        Assert.Equal(["ideal", "ideal", "ideal"], third.Strikes.Select(strike => strike.Band));

        Assert.Equal("completed", third.Status);
        Assert.Equal("epic", third.Craftsmanship);
        Assert.NotNull(third.ItemId);
        Assert.False(third.Heating);

        var item = Assert.Single(await client.ReadItemsAsync(Cancellation));
        Assert.Equal(third.ItemId, item.Id);
        Assert.Equal("Sword", item.WeaponType);
        Assert.Equal("Sword", item.Name);
        Assert.Equal(ForgeApi.SwordRecipe, item.RecipeKey);
        Assert.Equal("epic", item.Craftsmanship);
        Assert.Equal("ordinaryforge", item.Origin);
    }

    [Fact]
    public async Task Workable_but_never_ideal_timing_produces_a_rare_sword()
    {
        using var client = await SignedInAsync("forge-rare");

        await client.BeginAsync(Cancellation);
        await client.HeatAsync(true, Cancellation);

        await StrikeAfterAsync(client, 1.5);                  // 0 -> 45
        await StrikeAfterAsync(client, 0.7);                  // 23 -> 44
        var last = await StrikeAfterAsync(client, 0.8);       // 22 -> 46

        Assert.Equal(["workable", "workable", "workable"], last.Strikes.Select(strike => strike.Band));
        Assert.Equal("rare", last.Craftsmanship);
    }

    /// <summary>
    /// Hammering cold iron. Canon asks routine forging to be forgiving, so this is a poor
    /// sword rather than no sword.
    /// </summary>
    [Fact]
    public async Task Striking_cold_iron_produces_a_common_sword_rather_than_a_failure()
    {
        using var client = await SignedInAsync("forge-common");

        await client.BeginAsync(Cancellation);

        await StrikeAfterAsync(client, 0.5);
        await StrikeAfterAsync(client, 0.5);
        var last = await StrikeAfterAsync(client, 0.5);

        Assert.Equal(["cold", "cold", "cold"], last.Strikes.Select(strike => strike.Band));
        Assert.Equal("common", last.Craftsmanship);
        Assert.Equal("common", Assert.Single(await client.ReadItemsAsync(Cancellation)).Craftsmanship);
    }

    /// <summary>
    /// The client can put whatever it likes in the body. None of it is read: the request
    /// carries an intent, never a result.
    /// </summary>
    [Fact]
    public async Task A_client_cannot_claim_a_heat_band_a_craftsmanship_or_an_owner()
    {
        using var client = await SignedInAsync("forge-claims");
        var session = await client.GetSessionAsync(Cancellation);

        await client.BeginAsync(Cancellation);

        for (var blow = 0; blow < 3; blow++)
        {
            factory.Clock.AdvanceSeconds(0.5);
            var response = await client.PostAsync(
                "/api/forge/strike",
                new
                {
                    band = "ideal",
                    temperature = 75,
                    craftsmanship = "epic",
                    ownerUserId = Guid.CreateVersion7(),
                },
                Cancellation);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        var item = Assert.Single(await client.ReadItemsAsync(Cancellation));
        Assert.Equal("common", item.Craftsmanship);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<WeaponsOfOrderDbContext>();
        var stored = await db.ForgedItems.AsNoTracking().SingleAsync(row => row.Id == item.Id, Cancellation);

        Assert.Equal(session.AccountId, stored.OwnerUserId);
        Assert.Equal(Craftsmanship.Common, stored.Craftsmanship);
    }

    [Fact]
    public async Task A_second_blow_arriving_inside_the_cooldown_is_refused()
    {
        using var client = await SignedInAsync("forge-cooldown");

        await client.BeginAsync(Cancellation);
        await StrikeAfterAsync(client, 0.5);

        factory.Clock.AdvanceSeconds(0.2);
        var tooSoon = await client.PostStrikeAsync(Cancellation);

        Assert.Equal(HttpStatusCode.Conflict, tooSoon.StatusCode);
        Assert.Equal("forge_strike_cooldown", await TestAccounts.ReadProblemCodeAsync(tooSoon, Cancellation));

        var state = await client.ReadStateAsync(Cancellation);
        Assert.Equal(1, state.RequireSession().StrikesTaken);
    }

    [Fact]
    public async Task Striking_a_finished_workpiece_cannot_mint_a_second_item()
    {
        using var client = await SignedInAsync("forge-no-duplicate");

        await client.BeginAsync(Cancellation);
        await StrikeAfterAsync(client, 0.5);
        await StrikeAfterAsync(client, 0.5);
        await StrikeAfterAsync(client, 0.5);

        var again = await client.PostStrikeAsync(Cancellation);

        Assert.Equal(HttpStatusCode.Conflict, again.StatusCode);
        Assert.Equal("forge_not_active", await TestAccounts.ReadProblemCodeAsync(again, Cancellation));
        Assert.Single(await client.ReadItemsAsync(Cancellation));
    }

    [Fact]
    public async Task Three_simultaneous_blows_on_the_last_strike_still_produce_one_item()
    {
        using var client = await SignedInAsync("forge-strike-race");

        await client.BeginAsync(Cancellation);
        await StrikeAfterAsync(client, 0.5);
        await StrikeAfterAsync(client, 0.5);

        factory.Clock.AdvanceSeconds(0.5);
        var attempts = await Task.WhenAll(
            client.PostStrikeAsync(Cancellation),
            client.PostStrikeAsync(Cancellation),
            client.PostStrikeAsync(Cancellation));

        Assert.Equal(1, attempts.Count(response => response.IsSuccessStatusCode));
        Assert.Single(await client.ReadItemsAsync(Cancellation));
    }

    /// <summary>
    /// Left in the fire. The ruin is derived from the stored anchor, so it is already true
    /// when the state is read and is written down by the next thing the player does.
    /// </summary>
    [Fact]
    public async Task A_workpiece_left_burning_past_the_grace_period_is_ruined_and_makes_no_item()
    {
        using var client = await SignedInAsync("forge-ruin");

        await client.BeginAsync(Cancellation);
        await client.HeatAsync(true, Cancellation);

        // 85 is reached at 2.83s; three further seconds of burning is the configured grace.
        factory.Clock.AdvanceSeconds(6);

        var projected = (await client.ReadStateAsync(Cancellation)).RequireSession();
        Assert.Equal("ruined", projected.Status);
        Assert.True(projected.BurnSeconds >= 3);

        var afterStrike = (await client.StrikeAsync(Cancellation)).RequireSession();
        Assert.Equal("ruined", afterStrike.Status);
        Assert.Null(afterStrike.Craftsmanship);
        Assert.Null(afterStrike.ItemId);
        Assert.Empty(await client.ReadItemsAsync(Cancellation));

        // A ruined workpiece is not allowed to keep the anvil, so the next one can start.
        var restarted = await client.BeginAsync(Cancellation);
        Assert.Equal("active", restarted.RequireSession().Status);
        Assert.Equal(new MaterialsView(18, 10, 8), restarted.Materials);
    }

    [Fact]
    public async Task A_single_too_hot_blow_costs_quality_and_nothing_else()
    {
        using var client = await SignedInAsync("forge-too-hot");

        await client.BeginAsync(Cancellation);
        await client.HeatAsync(true, Cancellation);

        // 0 -> 90: burning, but only 0.17s of it, well inside the grace period.
        var burned = await StrikeAfterAsync(client, 3);
        Assert.Equal("burning", burned.Strikes[0].Band);
        Assert.Equal("active", burned.Status);

        await client.HeatAsync(false, Cancellation);
        await StrikeAfterAsync(client, 0.5);
        var last = await StrikeAfterAsync(client, 0.5);

        Assert.Equal("completed", last.Status);
        Assert.NotNull(last.ItemId);
    }

    [Fact]
    public async Task Setting_a_workpiece_aside_frees_the_anvil_without_returning_its_materials()
    {
        using var client = await SignedInAsync("forge-abandon");

        await client.BeginAsync(Cancellation);
        var abandoned = await client.PostAbandonAsync(Cancellation);
        Assert.Equal(HttpStatusCode.OK, abandoned.StatusCode);

        var state = await client.ReadStateAsync(Cancellation);
        Assert.Equal("abandoned", state.RequireSession().Status);
        Assert.Equal(new MaterialsView(21, 11, 8), state.Materials);
        Assert.Empty(await client.ReadItemsAsync(Cancellation));

        var restarted = await client.BeginAsync(Cancellation);
        Assert.Equal("active", restarted.RequireSession().Status);
        Assert.Equal(new MaterialsView(18, 10, 8), restarted.Materials);
    }

    /// <summary>
    /// The workpiece belongs to the account, not to the tab it was started in. A second
    /// browser holding the same session sees the same anvil, at the temperature the server
    /// says it is now.
    /// </summary>
    [Fact]
    public async Task An_unfinished_workpiece_is_still_there_for_a_new_browser()
    {
        var email = TestAccounts.NewEmail("forge-resume");
        using var first = factory.CreateAuthClient();
        await first.SignInAsNewAccountAsync(factory, email, TestAccounts.ValidPassword, Cancellation);

        await first.BeginAsync(Cancellation);
        var struck = await StrikeAfterAsync(first, 0.5);

        using var second = factory.CreateAuthClient();
        await second.SignInAsync(email, TestAccounts.ValidPassword, Cancellation);

        var resumed = await second.ReadStateAsync(Cancellation);
        Assert.Equal(struck.Id, resumed.RequireSession().Id);
        Assert.Equal(1, resumed.RequireSession().StrikesTaken);
        Assert.Equal(new MaterialsView(21, 11, 8), resumed.Materials);

        // And finishing it from the second browser produces exactly one item.
        await StrikeAfterAsync(second, 0.5);
        await StrikeAfterAsync(second, 0.5);
        Assert.Single(await second.ReadItemsAsync(Cancellation));
    }

    [Fact]
    public async Task A_forged_item_is_in_the_database_and_survives_a_new_session()
    {
        var email = TestAccounts.NewEmail("forge-persistence");
        using var client = factory.CreateAuthClient();
        await client.SignInAsNewAccountAsync(factory, email, TestAccounts.ValidPassword, Cancellation);
        var accountId = (await client.GetSessionAsync(Cancellation)).AccountId;

        await client.BeginAsync(Cancellation);
        await client.HeatAsync(true, Cancellation);
        await StrikeAfterAsync(client, 2.4);
        await StrikeAfterAsync(client, 0.8);
        var finished = await StrikeAfterAsync(client, 0.8);

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<WeaponsOfOrderDbContext>();
            var stored = await db.ForgedItems
                .AsNoTracking()
                .SingleAsync(item => item.ForgeSessionId == finished.Id, Cancellation);

            Assert.Equal(accountId, stored.OwnerUserId);
            Assert.Equal("Sword", stored.WeaponType);
            Assert.Equal(Craftsmanship.Epic, stored.Craftsmanship);
            Assert.Equal(ForgedItemOrigin.OrdinaryForge, stored.Origin);

            // The strikes that produced it are recorded, not just the verdict.
            var strikes = await db.ForgeStrikes
                .AsNoTracking()
                .Where(strike => strike.ForgeSessionId == finished.Id)
                .OrderBy(strike => strike.Ordinal)
                .ToListAsync(Cancellation);

            Assert.Equal([HeatBand.Ideal, HeatBand.Ideal, HeatBand.Ideal], strikes.Select(strike => strike.Band));
        }

        using var later = factory.CreateAuthClient();
        await later.SignInAsync(email, TestAccounts.ValidPassword, Cancellation);

        var item = Assert.Single(await later.ReadItemsAsync(Cancellation));
        Assert.Equal("epic", item.Craftsmanship);
    }

    [Fact]
    public async Task The_gameplay_schema_is_migrated_rather_than_assumed()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<WeaponsOfOrderDbContext>();

        // The prototype's history is a single SQLite baseline. It was rebuilt from the
        // current model when the provider changed, because there was no deployed data to
        // preserve and PostgreSQL-shaped migrations could not describe a SQLite database.
        var applied = await db.Database.GetAppliedMigrationsAsync(Cancellation);
        Assert.NotEmpty(applied);
        Assert.Empty(await db.Database.GetPendingMigrationsAsync(Cancellation));

        // Each gameplay table answers a query, which is the part a snapshot cannot prove.
        Assert.True(await db.PlayerMaterials.CountAsync(Cancellation) >= 0);
        Assert.True(await db.ForgeSessions.CountAsync(Cancellation) >= 0);
        Assert.True(await db.ForgeStrikes.CountAsync(Cancellation) >= 0);
        Assert.True(await db.ForgedItems.CountAsync(Cancellation) >= 0);
    }

    private async Task<AuthTestClient> SignedInAsync(string label)
    {
        var client = factory.CreateAuthClient();
        await client.SignInAsNewAccountAsync(
            factory,
            TestAccounts.NewEmail(label),
            TestAccounts.ValidPassword,
            Cancellation);

        return client;
    }

    private async Task<SessionView> StrikeAfterAsync(AuthTestClient client, double seconds)
    {
        factory.Clock.AdvanceSeconds(seconds);
        return (await client.StrikeAsync(Cancellation)).RequireSession();
    }

    private async Task<int> CountSessionsAsync(AuthTestClient client)
    {
        var accountId = (await client.GetSessionAsync(Cancellation)).AccountId;

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<WeaponsOfOrderDbContext>();

        return await db.ForgeSessions.CountAsync(session => session.OwnerUserId == accountId, Cancellation);
    }
}

/// <summary>
/// An account with nothing to forge with, which is the only way to prove the cost is checked
/// rather than assumed.
/// </summary>
public sealed class EmptyStockForgeTests(EmptyStockForgeApiFactory factory)
    : IClassFixture<EmptyStockForgeApiFactory>
{
    private CancellationToken Cancellation => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Beginning_a_forge_without_the_materials_is_refused_and_starts_nothing()
    {
        using var client = factory.CreateAuthClient();
        await client.SignInAsNewAccountAsync(
            factory,
            TestAccounts.NewEmail("forge-poor"),
            TestAccounts.ValidPassword,
            Cancellation);

        var before = await client.ReadStateAsync(Cancellation);
        Assert.Equal(new MaterialsView(0, 0, 0), before.Materials);
        Assert.False(Assert.Single(before.Recipes).Affordable);

        var response = await client.PostBeginAsync(Cancellation);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("forge_insufficient_materials", await TestAccounts.ReadProblemCodeAsync(response, Cancellation));

        var after = await client.ReadStateAsync(Cancellation);
        Assert.Equal(new MaterialsView(0, 0, 0), after.Materials);
        Assert.Null(after.Session);
    }
}
