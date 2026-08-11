using WeaponsOfOrder.Api.Health;
using WeaponsOfOrder.Infrastructure;
using WeaponsOfOrder.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddProblemDetails();
builder.Services.AddWeaponsOfOrderPersistence(builder.Configuration);

builder.Services
    .AddHealthChecks()
    .AddDbContextCheck<WeaponsOfOrderDbContext>("database", tags: [HealthEndpoints.ReadinessTag]);

var app = builder.Build();

app.UseExceptionHandler();
app.UseStatusCodePages();

app.MapWeaponsOfOrderHealthChecks();

// Unmatched /api routes stay 404s instead of falling through to the SPA document.
app.MapFallback("/api/{**rest}", () => Results.NotFound());

// Single public origin: deployed environments serve the built React client from wwwroot.
// It is absent during development — Vite serves the client on :1337 and proxies /api
// here, which keeps the browser on one origin there too — so the static-file pipeline is
// only wired up when the directory actually exists.
if (Directory.Exists(Path.Combine(app.Environment.ContentRootPath, "wwwroot")))
{
    app.UseDefaultFiles();
    app.UseStaticFiles();
    app.MapFallbackToFile("index.html");
}

// Migrations are applied explicitly (`dotnet ef database update`), never on startup:
// automatic migration on boot is unsafe once more than one instance runs.
app.Run();

/// <summary>Exposed so the API test project can host the real pipeline.</summary>
public partial class Program;
