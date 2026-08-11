using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace WeaponsOfOrder.Api.Health;

/// <summary>
/// The integration seam the browser client uses to confirm it is talking to this API.
/// </summary>
internal static class HealthEndpoints
{
    public const string ReadinessTag = "ready";

    public const string ServiceName = "weapons-of-order-api";

    public static IEndpointRouteBuilder MapWeaponsOfOrderHealthChecks(this IEndpointRouteBuilder endpoints)
    {
        // Liveness deliberately excludes the database: a database outage means "not
        // ready", not "restart the process".
        endpoints.MapHealthChecks("/api/health", new HealthCheckOptions
        {
            Predicate = _ => false,
            ResponseWriter = WriteResponseAsync,
        });

        endpoints.MapHealthChecks("/api/health/ready", new HealthCheckOptions
        {
            Predicate = registration => registration.Tags.Contains(ReadinessTag),
            ResponseWriter = WriteResponseAsync,
        });

        return endpoints;
    }

    /// <summary>
    /// Reports check names and statuses only. Descriptions and exceptions are withheld so
    /// a public endpoint cannot leak connection strings or stack traces.
    /// </summary>
    private static Task WriteResponseAsync(HttpContext context, HealthReport report)
    {
        var payload = new HealthResponse(
            ServiceName,
            report.Status.ToString(),
            report.Entries.ToDictionary(entry => entry.Key, entry => entry.Value.Status.ToString()));

        return context.Response.WriteAsJsonAsync(payload);
    }

    private sealed record HealthResponse(string Service, string Status, IDictionary<string, string> Checks);
}
