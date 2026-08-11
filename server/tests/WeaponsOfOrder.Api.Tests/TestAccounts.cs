using System.Text.Json;
using System.Text.Json.Nodes;

namespace WeaponsOfOrder.Api.Tests;

/// <summary>
/// The test classes share one database, so every test mints its own address rather than
/// relying on a clean table.
/// </summary>
internal static class TestAccounts
{
    /// <summary>Comfortably over the 12-character policy configured in appsettings.json.</summary>
    public const string ValidPassword = "forge-anvil-ember-73";

    public static string NewEmail(string label) => $"{label}-{Guid.CreateVersion7():n}@weaponsoforder.test";

    /// <summary>
    /// Buffers the body first so a test can assert on several parts of one response.
    /// </summary>
    public static async Task<JsonElement> ReadBodyAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        var json = await response.Content.ReadAsStringAsync(cancellationToken);
        using var document = JsonDocument.Parse(json);
        return document.RootElement.Clone();
    }

    /// <summary>The machine-readable <c>code</c> extension carried by every API problem response.</summary>
    public static async Task<string?> ReadProblemCodeAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        var body = await ReadBodyAsync(response, cancellationToken);
        return body.TryGetProperty("code", out var code) ? code.GetString() : null;
    }

    /// <summary>
    /// The response body with its trace identifier removed, for the non-enumeration tests
    /// that assert two answers are indistinguishable. <c>traceId</c> is per-request
    /// diagnostics and carries nothing about the account.
    /// </summary>
    public static async Task<string> ReadComparableBodyAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        var json = await response.Content.ReadAsStringAsync(cancellationToken);
        if (JsonNode.Parse(json) is not JsonObject body)
        {
            return json;
        }

        body.Remove("traceId");
        return body.ToJsonString();
    }
}
