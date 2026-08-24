using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace WeaponsOfOrder.Api.Tests;

public sealed record HexView(int Column, int Row);

public sealed record CombatStatsView(
    int Hp,
    int Power,
    int Defense,
    double AttackIntervalSeconds,
    double CriticalChance,
    int Range,
    bool Mounted);

public sealed record ArmyWeaponView(Guid ItemId, string Name, string Craftsmanship);

public sealed record ArmyUnitView(
    Guid UnitId,
    string DefinitionKey,
    string Name,
    string Kingdom,
    int Tier,
    bool Mounted,
    IReadOnlyList<ArmyWeaponView> Weapons,
    CombatStatsView Stats,
    string Role,
    HexView? Hex,
    int? ReserveOrder,
    HexView? ReserveEntryHex);

public sealed record BattlefieldView(int Columns, int Rows, int DeploymentColumns);

public sealed record ArmyLimitsView(int Active, int Reserve, int Army);

public sealed record ArmyView(
    BattlefieldView Battlefield,
    ArmyLimitsView Limits,
    IReadOnlyList<ArmyUnitView> Units,
    bool Ready);

public sealed record BattleCombatantView(
    string Id,
    string Side,
    Guid? UnitId,
    string Name,
    CombatStatsView Stats,
    int? ReserveOrder,
    HexView? ReserveEntryHex,
    string EndState,
    int FinalHp,
    int FinalEnergy,
    HexView? FinalHex);

/// <summary>
/// A resolved battle as the browser receives it.
/// </summary>
/// <remarks>
/// Events stay as raw JSON. They are a discriminated union on <c>kind</c>, and reading them as
/// elements lets a test assert on the wire shape itself rather than on a set of records that
/// could drift from it.
/// </remarks>
public sealed record BattleResultView(
    string Outcome,
    string Reason,
    int DurationMilliseconds,
    string Seed,
    BattlefieldView Battlefield,
    IReadOnlyList<BattleCombatantView> Combatants,
    IReadOnlyList<JsonElement> Events);

/// <summary>Drives the army and battle API the way the browser client does.</summary>
public static class BattleApi
{
    public const string ArmyPath = "/api/battle/army";
    public const string SimulatePath = "/api/battle/simulate";

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public static async Task<ArmyView> ReadArmyAsync(this AuthTestClient client, CancellationToken cancellationToken)
    {
        var response = await client.Http.GetAsync(ArmyPath, cancellationToken);
        await AssertSucceededAsync(response, cancellationToken);

        return await ReadAsync<ArmyView>(response, cancellationToken);
    }

    public static Task<HttpResponseMessage> PostArmyAsync(
        this AuthTestClient client,
        object body,
        CancellationToken cancellationToken)
        => client.PostAsync(ArmyPath, body, cancellationToken);

    public static async Task<ArmyView> SaveArmyAsync(
        this AuthTestClient client,
        object body,
        CancellationToken cancellationToken)
    {
        var response = await client.PostArmyAsync(body, cancellationToken);
        await AssertSucceededAsync(response, cancellationToken);

        return await ReadAsync<ArmyView>(response, cancellationToken);
    }

    /// <summary>Deploys the caller's Units along their own front column, in roster order.</summary>
    public static Task<ArmyView> DeployEveryUnitAsync(
        this AuthTestClient client,
        ArmyView army,
        CancellationToken cancellationToken)
        => client.SaveArmyAsync(
            new
            {
                active = army.Units
                    .Select((unit, index) => new
                    {
                        unitId = unit.UnitId,
                        column = army.Battlefield.DeploymentColumns - 1,
                        row = index,
                    })
                    .ToArray(),
                reserves = Array.Empty<Guid>(),
            },
            cancellationToken);

    public static Task<HttpResponseMessage> PostSimulateAsync(
        this AuthTestClient client,
        CancellationToken cancellationToken)
        => client.PostAsync(SimulatePath, new { }, cancellationToken);

    public static async Task<BattleResultView> SimulateAsync(
        this AuthTestClient client,
        CancellationToken cancellationToken)
    {
        var response = await client.PostSimulateAsync(cancellationToken);
        await AssertSucceededAsync(response, cancellationToken);

        return await ReadAsync<BattleResultView>(response, cancellationToken);
    }

    /// <summary>The caller's Unit for a definition key, asserted to exist.</summary>
    public static ArmyUnitView Unit(this ArmyView army, string definitionKey)
        => Assert.Single(army.Units, unit => unit.DefinitionKey == definitionKey);

    /// <summary>Every event of one kind, in order.</summary>
    public static IReadOnlyList<JsonElement> Events(this BattleResultView result, string kind)
        => [.. result.Events.Where(moment => moment.GetProperty("kind").GetString() == kind)];

    public static string Text(this JsonElement moment, string property)
        => moment.GetProperty(property).GetString() ?? string.Empty;

    public static int Number(this JsonElement moment, string property)
        => moment.GetProperty(property).GetInt32();

    /// <summary>The problem code the server answered with.</summary>
    public static async Task<string> ProblemCodeAsync(
        this HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);

        return body.TryGetProperty("code", out var code) ? code.GetString() ?? string.Empty : string.Empty;
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
            ?? throw new InvalidOperationException("The battle API returned an empty body.");
}
