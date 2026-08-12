using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit;

namespace WeaponsOfOrder.Api.Tests;

/// <summary>
/// A clock the test drives by hand.
/// </summary>
/// <remarks>
/// The forge decides what a strike was worth from elapsed time, so a test has to be able to
/// say "two and a half seconds passed" without two and a half seconds passing. Every timing
/// assertion in these tests is exact for that reason, and none of them sleeps.
/// </remarks>
public sealed class ForgeTestClock : TimeProvider
{
    private long _ticks = DateTimeOffset.UtcNow.UtcTicks;

    public override DateTimeOffset GetUtcNow() => new(Interlocked.Read(ref _ticks), TimeSpan.Zero);

    public void AdvanceSeconds(double seconds)
        => Interlocked.Add(ref _ticks, (long)Math.Round(seconds * TimeSpan.TicksPerSecond));
}

/// <summary>The API with its clock under test control.</summary>
public class ForgeApiFactory : WeaponsOfOrderApiFactory
{
    public ForgeTestClock Clock { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);

        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<TimeProvider>();
            services.AddSingleton<TimeProvider>(Clock);
        });
    }
}

/// <summary>An account that owns nothing, to prove the forge refuses work it cannot pay for.</summary>
public sealed class EmptyStockForgeApiFactory : ForgeApiFactory
{
    protected override IEnumerable<KeyValuePair<string, string?>> AdditionalConfiguration =>
    [
        new("Forge:StartingMaterials:Metal", "0"),
        new("Forge:StartingMaterials:Wood", "0"),
        new("Forge:StartingMaterials:Leather", "0"),
    ];
}

public sealed record MaterialsView(int Metal, int Wood, int Leather);

public sealed record RecipeView(string Key, string Name, string WeaponType, MaterialsView Cost, bool Affordable);

public sealed record StrikeView(int Ordinal, string Band);

public sealed record SessionView(
    Guid Id,
    string RecipeKey,
    string Name,
    string WeaponType,
    string Status,
    double Temperature,
    string Band,
    bool Heating,
    DateTimeOffset ObservedAt,
    int StrikesTaken,
    int StrikesRequired,
    IReadOnlyList<StrikeView> Strikes,
    double BurnSeconds,
    string? Craftsmanship,
    Guid? ItemId);

public sealed record ForgeStateView(
    MaterialsView Materials,
    IReadOnlyList<RecipeView> Recipes,
    SessionView? Session);

public sealed record ItemView(
    Guid Id,
    string RecipeKey,
    string Name,
    string WeaponType,
    string Craftsmanship,
    string Origin,
    DateTimeOffset ForgedAt);

/// <summary>
/// Drives the forge API the way the browser client does: read state, begin, hold the piece
/// in the fire, strike.
/// </summary>
public static class ForgeApi
{
    /// <summary>The one recipe Task 4 ships. Matches the key in <c>appsettings.json</c>.</summary>
    public const string SwordRecipe = "weapon.sword";

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public static async Task<ForgeStateView> ReadStateAsync(
        this AuthTestClient client,
        CancellationToken cancellationToken)
    {
        var response = await client.Http.GetAsync("/api/forge/state", cancellationToken);
        response.EnsureSuccessStatusCode();
        return await ReadAsync<ForgeStateView>(response, cancellationToken);
    }

    public static Task<HttpResponseMessage> PostBeginAsync(
        this AuthTestClient client,
        CancellationToken cancellationToken,
        string recipeKey = SwordRecipe)
        => client.PostAsync("/api/forge/begin", new { recipeKey }, cancellationToken);

    public static Task<HttpResponseMessage> PostHeatAsync(
        this AuthTestClient client,
        bool heating,
        CancellationToken cancellationToken)
        => client.PostAsync("/api/forge/heat", new { heating }, cancellationToken);

    public static Task<HttpResponseMessage> PostStrikeAsync(
        this AuthTestClient client,
        CancellationToken cancellationToken)
        => client.PostAsync("/api/forge/strike", new { }, cancellationToken);

    public static Task<HttpResponseMessage> PostAbandonAsync(
        this AuthTestClient client,
        CancellationToken cancellationToken)
        => client.PostAsync("/api/forge/abandon", new { }, cancellationToken);

    public static async Task<ForgeStateView> BeginAsync(
        this AuthTestClient client,
        CancellationToken cancellationToken,
        string recipeKey = SwordRecipe)
        => await Succeeding(await client.PostBeginAsync(cancellationToken, recipeKey), cancellationToken);

    public static async Task<ForgeStateView> HeatAsync(
        this AuthTestClient client,
        bool heating,
        CancellationToken cancellationToken)
        => await Succeeding(await client.PostHeatAsync(heating, cancellationToken), cancellationToken);

    public static async Task<ForgeStateView> StrikeAsync(
        this AuthTestClient client,
        CancellationToken cancellationToken)
        => await Succeeding(await client.PostStrikeAsync(cancellationToken), cancellationToken);

    public static async Task<IReadOnlyList<ItemView>> ReadItemsAsync(
        this AuthTestClient client,
        CancellationToken cancellationToken)
    {
        var response = await client.Http.GetAsync("/api/forge/items", cancellationToken);
        response.EnsureSuccessStatusCode();
        return await ReadAsync<IReadOnlyList<ItemView>>(response, cancellationToken);
    }

    /// <summary>The session, asserted to exist. Every caller here has just made one.</summary>
    public static SessionView RequireSession(this ForgeStateView state)
    {
        Assert.NotNull(state.Session);
        return state.Session;
    }

    private static async Task<ForgeStateView> Succeeding(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        Assert.True(
            response.IsSuccessStatusCode,
            $"{(int)response.StatusCode} {await response.Content.ReadAsStringAsync(cancellationToken)}");

        return await ReadAsync<ForgeStateView>(response, cancellationToken);
    }

    private static async Task<T> ReadAsync<T>(HttpResponseMessage response, CancellationToken cancellationToken)
        => await response.Content.ReadFromJsonAsync<T>(Json, cancellationToken)
            ?? throw new InvalidOperationException("The forge API returned an empty body.");
}
