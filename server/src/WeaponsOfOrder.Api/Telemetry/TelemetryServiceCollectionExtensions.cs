using Azure.Monitor.OpenTelemetry.AspNetCore;
using OpenTelemetry;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using WeaponsOfOrder.Api.Health;

namespace WeaponsOfOrder.Api.Telemetry;

/// <summary>
/// Server telemetry through Azure Monitor.
/// </summary>
/// <remarks>
/// Uses the OpenTelemetry-based Azure Monitor distro, which is Microsoft's current
/// recommendation, rather than the classic Application Insights SDK. Registered only when a
/// connection string is configured, so local development and the test host stay free of an
/// exporter with nowhere to send.
/// </remarks>
internal static class TelemetryServiceCollectionExtensions
{
    /// <summary>
    /// The name App Service sets when Application Insights is attached to the site. Read
    /// as-is so the platform's own setting is enough, with the nested
    /// <c>ApplicationInsights:ConnectionString</c> form available for anything else.
    /// </summary>
    private const string PlatformConnectionStringKey = "APPLICATIONINSIGHTS_CONNECTION_STRING";

    private const string ConfiguredConnectionStringKey = "ApplicationInsights:ConnectionString";

    public static IServiceCollection AddWeaponsOfOrderTelemetry(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration[ConfiguredConnectionStringKey]
            ?? configuration[PlatformConnectionStringKey];

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return services;
        }

        services.AddOpenTelemetry()
            .ConfigureResource(resource => resource.AddService(HealthEndpoints.ServiceName))
            .WithTracing(tracing => tracing
                // Last in the pipeline, so it sees every span from every instrumentation
                // this or the distro registers, including ones added later.
                //
                // No database instrumentation is registered: the store is a SQLite file in
                // this process, so a query is not a network call and there is no dependency
                // span to collect. A slow one shows up as a slow request.
                .AddProcessor(new QueryStringRedactingProcessor()))
            .UseAzureMonitor(options => options.ConnectionString = connectionString);

        return services;
    }
}
