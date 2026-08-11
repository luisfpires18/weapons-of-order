using Microsoft.AspNetCore.Mvc.Testing;

namespace WeaponsOfOrder.Api.Tests;

/// <summary>
/// Hosts the real API pipeline in memory. A syntactically valid connection string is
/// supplied so startup succeeds; no test in this project opens a database connection
/// except the readiness check, which is asserted on shape rather than verdict.
/// </summary>
public sealed class WeaponsOfOrderApiFactory : WebApplicationFactory<Program>
{
    private const string TestConnectionString =
        "Host=localhost;Port=5433;Database=weapons_of_order_tests;Username=woo_dev;Password=woo_dev";

    public WeaponsOfOrderApiFactory()
    {
        // WebApplicationFactory's ConfigureAppConfiguration hooks only apply once the host
        // is built, which is after Program's top-level statements have already read
        // builder.Configuration. Environment variables are the seam that lands in time.
        Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", "Production");
        Environment.SetEnvironmentVariable("ConnectionStrings__WeaponsOfOrder", TestConnectionString);
    }
}
