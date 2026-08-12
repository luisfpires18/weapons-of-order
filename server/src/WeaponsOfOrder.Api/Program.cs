using WeaponsOfOrder.Api.Auth;
using WeaponsOfOrder.Api.Auth.Notifications;
using WeaponsOfOrder.Api.Forge;
using WeaponsOfOrder.Api.Health;
using WeaponsOfOrder.Infrastructure;
using WeaponsOfOrder.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddProblemDetails();
builder.Services.AddWeaponsOfOrderPersistence(builder.Configuration);
builder.Services.AddWeaponsOfOrderAuth(builder.Configuration, builder.Environment);
builder.Services.AddWeaponsOfOrderForge(builder.Configuration);

builder.Services
    .AddHealthChecks()
    .AddDbContextCheck<WeaponsOfOrderDbContext>("database", tags: [HealthEndpoints.ReadinessTag]);

var app = builder.Build();

// ProblemDetails for both thrown exceptions and bare status codes, so nothing reaches a
// player as a stack trace or as an empty body the client cannot interpret.
app.UseExceptionHandler();
app.UseStatusCodePages();

// Before authentication: a credential-stuffing run should be turned away without the
// server ever hashing a password.
app.UseRateLimiter();

app.UseAuthentication();
app.UseAuthorization();

// A no-op for the JSON endpoints here, which validate through the antiforgery endpoint
// filter instead. It is in the pipeline so a future form-binding endpoint is covered by
// the framework's own check rather than silently unprotected.
app.UseAntiforgery();

app.MapWeaponsOfOrderHealthChecks();
app.MapWeaponsOfOrderAuth();
app.MapWeaponsOfOrderForge();

if (app.Environment.IsDevelopment())
{
    app.MapDevelopmentAccountNotifications();
}

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
