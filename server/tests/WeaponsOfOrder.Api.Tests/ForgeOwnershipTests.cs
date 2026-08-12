using System.Net;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using WeaponsOfOrder.Infrastructure.Persistence;
using Xunit;

namespace WeaponsOfOrder.Api.Tests;

/// <summary>
/// Two accounts, one server. Ownership comes from the session cookie and from nowhere else.
/// </summary>
/// <remarks>
/// There is no route that takes an item id, a session id or an owner, which is the strongest
/// form this guarantee can take: a guessed identifier has nothing to be handed to. What
/// these tests hold in place is that the routes which do exist are scoped to the caller, so
/// adding an item-detail route in Task 5 starts from a boundary that is already proven.
/// </remarks>
public sealed class ForgeOwnershipTests(ForgeApiFactory factory) : IClassFixture<ForgeApiFactory>
{
    private CancellationToken Cancellation => TestContext.Current.CancellationToken;

    [Fact]
    public async Task One_players_workpiece_is_invisible_to_another()
    {
        using var smith = await SignedInAsync("owner-smith");
        using var stranger = await SignedInAsync("owner-stranger");

        await smith.BeginAsync(Cancellation);

        var strangerState = await stranger.ReadStateAsync(Cancellation);

        Assert.Null(strangerState.Session);
        // Untouched stock: the stranger has been charged for nothing.
        Assert.Equal(new MaterialsView(24, 12, 8), strangerState.Materials);
    }

    [Fact]
    public async Task Another_account_cannot_heat_strike_or_set_aside_a_workpiece_that_is_not_theirs()
    {
        using var smith = await SignedInAsync("owner-act-smith");
        using var stranger = await SignedInAsync("owner-act-stranger");

        var session = (await smith.BeginAsync(Cancellation)).RequireSession();

        foreach (var attempt in new[]
        {
            await stranger.PostHeatAsync(true, Cancellation),
            await stranger.PostStrikeAsync(Cancellation),
            await stranger.PostAbandonAsync(Cancellation),
        })
        {
            Assert.Equal(HttpStatusCode.Conflict, attempt.StatusCode);
            Assert.Equal("forge_not_active", await TestAccounts.ReadProblemCodeAsync(attempt, Cancellation));
        }

        // The owner's workpiece is exactly as they left it.
        var owned = (await smith.ReadStateAsync(Cancellation)).RequireSession();
        Assert.Equal(session.Id, owned.Id);
        Assert.Equal("active", owned.Status);
        Assert.Equal(0, owned.StrikesTaken);
        Assert.False(owned.Heating);
    }

    [Fact]
    public async Task A_forged_item_is_listed_only_for_the_account_that_forged_it()
    {
        using var smith = await SignedInAsync("owner-item-smith");
        using var stranger = await SignedInAsync("owner-item-stranger");

        await smith.BeginAsync(Cancellation);
        for (var blow = 0; blow < 3; blow++)
        {
            factory.Clock.AdvanceSeconds(0.5);
            await smith.StrikeAsync(Cancellation);
        }

        var forged = Assert.Single(await smith.ReadItemsAsync(Cancellation));
        Assert.Empty(await stranger.ReadItemsAsync(Cancellation));

        // And the row itself carries the owner the server chose, not one a caller supplied.
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<WeaponsOfOrderDbContext>();
        var stored = await db.ForgedItems.AsNoTracking().SingleAsync(item => item.Id == forged.Id, Cancellation);

        Assert.Equal((await smith.GetSessionAsync(Cancellation)).AccountId, stored.OwnerUserId);
        Assert.NotEqual((await stranger.GetSessionAsync(Cancellation)).AccountId, stored.OwnerUserId);
    }

    [Fact]
    public async Task Two_accounts_can_each_hold_their_own_workpiece_at_the_same_time()
    {
        using var first = await SignedInAsync("owner-parallel-first");
        using var second = await SignedInAsync("owner-parallel-second");

        var one = (await first.BeginAsync(Cancellation)).RequireSession();
        var other = (await second.BeginAsync(Cancellation)).RequireSession();

        Assert.NotEqual(one.Id, other.Id);

        // The one-active-forge rule is per account, not per server.
        await first.HeatAsync(true, Cancellation);
        factory.Clock.AdvanceSeconds(2.4);

        var heated = (await first.ReadStateAsync(Cancellation)).RequireSession();
        var untouched = (await second.ReadStateAsync(Cancellation)).RequireSession();

        Assert.Equal("ideal", heated.Band);
        Assert.Equal("cold", untouched.Band);
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
}
