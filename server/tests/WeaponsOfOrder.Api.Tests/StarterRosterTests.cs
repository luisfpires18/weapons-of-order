using System.Net;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using WeaponsOfOrder.Infrastructure.Gameplay;
using WeaponsOfOrder.Infrastructure.Persistence;
using Xunit;

namespace WeaponsOfOrder.Api.Tests;

/// <summary>
/// The temporary starter roster: one unit per configured starter definition, granted once.
/// </summary>
public sealed class StarterRosterTests(PreparationApiFactory factory) : IClassFixture<PreparationApiFactory>
{
    private CancellationToken Cancellation => TestContext.Current.CancellationToken;

    [Fact]
    public async Task An_authenticated_account_receives_exactly_one_unit_per_configured_starter()
    {
        using var client = await factory.SignedInAsync("starter-grant", Cancellation);

        var units = await client.ReadUnitsAsync(Cancellation);

        Assert.Equal(3, units.Count);
        Assert.Equal(3, units.Select(unit => unit.Id).Distinct().Count());
        Assert.All(units, unit => Assert.NotEqual(Guid.Empty, unit.Id));
    }

    [Fact]
    public async Task Reading_the_roster_again_does_not_grant_a_second_set()
    {
        using var client = await factory.SignedInAsync("starter-idempotent", Cancellation);

        var first = await client.ReadUnitsAsync(Cancellation);
        await client.ReadUnitsAsync(Cancellation);
        var third = await client.ReadUnitsAsync(Cancellation);

        // Same instances, same identifiers. Re-reading is not an acquisition.
        Assert.Equal(first.Select(unit => unit.Id), third.Select(unit => unit.Id));
    }

    [Fact]
    public async Task A_new_session_for_the_same_account_sees_the_same_unit_instances()
    {
        var email = TestAccounts.NewEmail("starter-restart");

        using var first = factory.CreateAuthClient();
        await first.SignInAsNewAccountAsync(factory, email, TestAccounts.ValidPassword, Cancellation);
        var granted = await first.ReadUnitsAsync(Cancellation);

        // A second browser: new cookie jar, same account, same server-side state.
        using var second = factory.CreateAuthClient();
        await second.SignInAsync(email, TestAccounts.ValidPassword, Cancellation);

        Assert.Equal(granted.Select(unit => unit.Id), (await second.ReadUnitsAsync(Cancellation)).Select(unit => unit.Id));
    }

    [Fact]
    public async Task Simultaneous_first_reads_still_grant_one_roster()
    {
        // Repeated over several fresh accounts on purpose. Both requests find no units and
        // both try to grant, and the interleaving that goes wrong is not the common one: when
        // the grants were written as a single transaction the two requests each held index
        // entries the other was waiting for, and the database broke the tie by failing one of
        // them. One account would usually get away with it.
        for (var attempt = 0; attempt < 5; attempt++)
        {
            using var client = await factory.SignedInAsync($"starter-race-{attempt}", Cancellation);

            var rosters = await Task.WhenAll(
                client.ReadUnitsAsync(Cancellation),
                client.ReadUnitsAsync(Cancellation));

            Assert.All(rosters, roster => Assert.Equal(3, roster.Count));
            Assert.Equal(rosters[0].Select(unit => unit.Id), rosters[1].Select(unit => unit.Id));
        }
    }

    [Fact]
    public async Task Each_account_receives_its_own_roster()
    {
        using var first = await factory.SignedInAsync("starter-mine", Cancellation);
        using var second = await factory.SignedInAsync("starter-yours", Cancellation);

        var mine = await first.ReadUnitsAsync(Cancellation);
        var theirs = await second.ReadUnitsAsync(Cancellation);

        Assert.Empty(mine.Select(unit => unit.Id).Intersect(theirs.Select(unit => unit.Id)));
    }

    [Fact]
    public async Task The_grant_is_recorded_apart_from_the_definition_so_duplicates_stay_possible()
    {
        using var client = await factory.SignedInAsync("starter-duplicates", Cancellation);

        var units = await client.ReadUnitsAsync(Cancellation);
        var ownerId = (await client.GetSessionAsync(Cancellation)).AccountId;

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<WeaponsOfOrderDbContext>();

        var granted = await db.PlayerUnits.AsNoTracking()
            .Where(unit => unit.OwnerUserId == ownerId)
            .ToListAsync(Cancellation);

        Assert.All(granted, unit => Assert.Equal(PlayerUnitOrigin.StarterGrant, unit.Origin));
        Assert.All(granted, unit => Assert.Equal(unit.DefinitionKey, unit.StarterGrantKey));

        // Recruitment does not exist yet, but the schema must not have decided against it. A
        // second copy of a definition with no grant recorded is accepted, which is what keeps
        // canon's "Regular Units may have multiple copies" reachable without a redesign.
        db.PlayerUnits.Add(new PlayerUnit
        {
            Id = Guid.CreateVersion7(),
            OwnerUserId = ownerId!.Value,
            DefinitionKey = PreparationApi.MeleeKey,
            StarterGrantKey = null,
            Origin = PlayerUnitOrigin.StarterGrant,
            AcquiredAt = DateTimeOffset.UtcNow,
        });

        await db.SaveChangesAsync(Cancellation);

        var afterwards = await client.ReadUnitsAsync(Cancellation);

        Assert.Equal(units.Count + 1, afterwards.Count);
        Assert.Equal(2, afterwards.Count(unit => unit.DefinitionKey == PreparationApi.MeleeKey));

        // And re-reading still does not re-grant the starter, even with a duplicate present.
        Assert.Equal(afterwards.Count, (await client.ReadUnitsAsync(Cancellation)).Count);
    }

    [Fact]
    public async Task A_saved_unit_whose_definition_has_gone_is_reported_rather_than_resolved_to_another()
    {
        using var client = await factory.SignedInAsync("starter-orphan", Cancellation);
        await client.ReadUnitsAsync(Cancellation);

        var ownerId = (await client.GetSessionAsync(Cancellation)).AccountId!.Value;

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<WeaponsOfOrderDbContext>();
            db.PlayerUnits.Add(new PlayerUnit
            {
                Id = Guid.CreateVersion7(),
                OwnerUserId = ownerId,
                DefinitionKey = "arkazia.removed",
                StarterGrantKey = null,
                Origin = PlayerUnitOrigin.StarterGrant,
                AcquiredAt = DateTimeOffset.UtcNow,
            });

            await db.SaveChangesAsync(Cancellation);
        }

        var response = await client.GetUnitsAsync(Cancellation);

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Equal("unit_definition_missing", await TestAccounts.ReadProblemCodeAsync(response, Cancellation));

        var body = await TestAccounts.ReadBodyAsync(response, Cancellation);
        Assert.Contains("arkazia.removed", body.GetProperty("detail").GetString()!, StringComparison.Ordinal);
    }
}

/// <summary>
/// A definition renamed in content, read back against units that already exist.
/// </summary>
public sealed class UnitContentIsEditableTests(
    PreparationApiFactory authored,
    RenamedUnitApiFactory renamed)
    : IClassFixture<PreparationApiFactory>, IClassFixture<RenamedUnitApiFactory>
{
    private CancellationToken Cancellation => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Renaming_a_definition_renames_units_that_already_exist_with_no_migration()
    {
        var email = TestAccounts.NewEmail("content-rename");

        using var before = authored.CreateAuthClient();
        await before.SignInAsNewAccountAsync(authored, email, TestAccounts.ValidPassword, Cancellation);

        var original = await before.UnitAsync(PreparationApi.MeleeKey, Cancellation);
        Assert.Equal("Melee", original.Name);

        // Same account, same database, same persistent row — one edited content value.
        using var after = renamed.CreateAuthClient();
        await after.SignInAsync(email, TestAccounts.ValidPassword, Cancellation);

        var resolved = await after.UnitAsync(PreparationApi.MeleeKey, Cancellation);

        Assert.Equal(RenamedUnitApiFactory.NewDisplayName, resolved.Name);
        Assert.Equal(original.Id, resolved.Id);
    }
}
