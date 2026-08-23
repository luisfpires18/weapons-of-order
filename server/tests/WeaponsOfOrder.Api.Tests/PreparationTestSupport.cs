using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace WeaponsOfOrder.Api.Tests;

public sealed record EquippedOnView(Guid UnitId, string UnitName, IReadOnlyList<int> Slots);

public sealed record InventoryItemView(
    Guid Id,
    string Name,
    string WeaponType,
    string Craftsmanship,
    string Origin,
    DateTimeOffset ForgedAt,
    int? SlotCost,
    bool Equippable,
    EquippedOnView? EquippedOn);

public sealed record UnitWeaponView(
    Guid ItemId,
    string Name,
    string WeaponType,
    string Craftsmanship,
    IReadOnlyList<int> Slots);

public sealed record UnitView(
    Guid Id,
    string DefinitionKey,
    string Name,
    string Type,
    string Kingdom,
    int Tier,
    string MaxArmor,
    bool Mounted,
    int WeaponSlots,
    IReadOnlyList<UnitWeaponView> Weapons);

/// <summary>
/// Drives the inventory and Units API the way the browser client does, and forges the swords
/// the equipment tests need through the real forge rather than inserting them.
/// </summary>
/// <remarks>
/// Forging for real is the point: Task 5 has to prove that what Task 4 persists is what a unit
/// picks up. No test here grants itself a weapon.
/// </remarks>
public static class PreparationApi
{
    public const string MeleeKey = "arkazia.melee";
    public const string RangedKey = "arkazia.ranged";
    public const string MountedKey = "arkazia.mounted";

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    /// <summary>
    /// A fresh confirmed account with its own cookie jar, as one browser would have.
    /// </summary>
    /// <remarks>
    /// The test classes share one database, so every test mints its own address rather than
    /// relying on a clean table.
    /// </remarks>
    public static async Task<AuthTestClient> SignedInAsync(
        this WeaponsOfOrderApiFactory factory,
        string label,
        CancellationToken cancellationToken)
    {
        var client = factory.CreateAuthClient();
        await client.SignInAsNewAccountAsync(
            factory,
            TestAccounts.NewEmail(label),
            TestAccounts.ValidPassword,
            cancellationToken);

        return client;
    }

    public static async Task<IReadOnlyList<UnitView>> ReadUnitsAsync(
        this AuthTestClient client,
        CancellationToken cancellationToken)
    {
        var response = await client.Http.GetAsync("/api/units", cancellationToken);
        await AssertSucceededAsync(response, cancellationToken);
        return await ReadAsync<IReadOnlyList<UnitView>>(response, cancellationToken);
    }

    public static Task<HttpResponseMessage> GetUnitsAsync(
        this AuthTestClient client,
        CancellationToken cancellationToken)
        => client.Http.GetAsync("/api/units", cancellationToken);

    public static async Task<IReadOnlyList<InventoryItemView>> ReadInventoryAsync(
        this AuthTestClient client,
        CancellationToken cancellationToken)
    {
        var response = await client.Http.GetAsync("/api/inventory/items", cancellationToken);
        await AssertSucceededAsync(response, cancellationToken);
        return await ReadAsync<IReadOnlyList<InventoryItemView>>(response, cancellationToken);
    }

    public static Task<HttpResponseMessage> PostEquipAsync(
        this AuthTestClient client,
        Guid unitId,
        Guid itemId,
        CancellationToken cancellationToken,
        int? slot = null)
        => client.PostAsync($"/api/units/{unitId}/equip", new { itemId, slot }, cancellationToken);

    public static Task<HttpResponseMessage> PostUnequipAsync(
        this AuthTestClient client,
        Guid unitId,
        Guid itemId,
        CancellationToken cancellationToken)
        => client.PostAsync($"/api/units/{unitId}/unequip", new { itemId }, cancellationToken);

    public static async Task<UnitView> EquipAsync(
        this AuthTestClient client,
        Guid unitId,
        Guid itemId,
        CancellationToken cancellationToken,
        int? slot = null)
        => await Succeeding(await client.PostEquipAsync(unitId, itemId, cancellationToken, slot), cancellationToken);

    public static async Task<UnitView> UnequipAsync(
        this AuthTestClient client,
        Guid unitId,
        Guid itemId,
        CancellationToken cancellationToken)
        => await Succeeding(await client.PostUnequipAsync(unitId, itemId, cancellationToken), cancellationToken);

    /// <summary>The caller's unit for a definition key, asserted to exist.</summary>
    public static async Task<UnitView> UnitAsync(
        this AuthTestClient client,
        string definitionKey,
        CancellationToken cancellationToken)
    {
        var units = await client.ReadUnitsAsync(cancellationToken);
        return Assert.Single(units, unit => unit.DefinitionKey == definitionKey);
    }

    /// <summary>
    /// Runs one real forge from start to finish and returns the item it produced.
    /// </summary>
    public static async Task<Guid> ForgeSwordAsync(
        this AuthTestClient client,
        ForgeApiFactory factory,
        CancellationToken cancellationToken)
    {
        await client.BeginAsync(cancellationToken);

        var state = await client.ReadStateAsync(cancellationToken);
        var required = state.RequireSession().StrikesRequired;

        for (var blow = 0; blow < required; blow++)
        {
            // Past the strike cooldown and nowhere near the burn grace period. The clock is
            // driven by hand, so nothing here sleeps.
            factory.Clock.AdvanceSeconds(0.5);
            state = await client.StrikeAsync(cancellationToken);
        }

        var session = state.RequireSession();
        Assert.Equal("completed", session.Status);
        Assert.NotNull(session.ItemId);

        return session.ItemId.Value;
    }

    public static IReadOnlyList<int> SlotsOf(this UnitView unit, Guid itemId)
        => unit.Weapons.SingleOrDefault(weapon => weapon.ItemId == itemId)?.Slots ?? [];

    private static async Task<UnitView> Succeeding(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        await AssertSucceededAsync(response, cancellationToken);
        return await ReadAsync<UnitView>(response, cancellationToken);
    }

    /// <summary>Fails with the server's own problem body rather than only a status code.</summary>
    private static async Task AssertSucceededAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
        => Assert.True(
            response.IsSuccessStatusCode,
            $"{(int)response.StatusCode} {await response.Content.ReadAsStringAsync(cancellationToken)}");

    private static async Task<T> ReadAsync<T>(HttpResponseMessage response, CancellationToken cancellationToken)
        => await response.Content.ReadFromJsonAsync<T>(Json, cancellationToken)
            ?? throw new InvalidOperationException("The preparation API returned an empty body.");
}
