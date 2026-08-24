using WeaponsOfOrder.Api.Auth;
using WeaponsOfOrder.Api.Auth.Notifications;
using WeaponsOfOrder.Api.Battle;
using WeaponsOfOrder.Api.Content;
using WeaponsOfOrder.Api.Forge;
using WeaponsOfOrder.Api.Health;
using WeaponsOfOrder.Api.Hosting;
using WeaponsOfOrder.Api.Preparation;
using WeaponsOfOrder.Api.Telemetry;
using WeaponsOfOrder.Infrastructure;
using WeaponsOfOrder.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

// Creator-authored game content from server/content, watched for changes. Kept out of
// appsettings.json because it is the creator's to edit, not the application's to configure.
builder.Configuration.AddWeaponsOfOrderContent(builder.Environment);

builder.Services.AddProblemDetails();
builder.Services.AddWeaponsOfOrderHosting(builder.Configuration);
builder.Services.AddWeaponsOfOrderTelemetry(builder.Configuration);
builder.Services.AddWeaponsOfOrderPersistence(builder.Configuration, builder.Environment.ContentRootPath);
builder.Services.AddWeaponsOfOrderAuth(builder.Configuration, builder.Environment);
builder.Services.AddWeaponsOfOrderGameContent(builder.Configuration);
builder.Services.AddWeaponsOfOrderForge(builder.Configuration);
builder.Services.AddWeaponsOfOrderPreparation();
builder.Services.AddWeaponsOfOrderBattle(builder.Configuration);

builder.Services
    .AddHealthChecks()
    .AddDbContextCheck<WeaponsOfOrderDbContext>("database", tags: [HealthEndpoints.ReadinessTag]);

var app = builder.Build();

// First, before anything reads the scheme or the caller's address. Behind App Service the
// real request is HTTPS from a browser, but this process is handed a plain HTTP one from a
// platform front end; everything below would otherwise reason about the front end instead
// of the player. No-op unless a deployment has declared it sits behind a trusted proxy.
app.UseWeaponsOfOrderForwardedHeaders();

if (!app.Environment.IsDevelopment())
{
    // Deployed environments are HTTPS-only. The platform already refuses plain HTTP, so
    // this is the browser-side half: after one visit it will not try http again, which
    // closes the window where a session cookie could be requested over the clear.
    app.UseHsts();
}

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
app.MapWeaponsOfOrderPreparation();
app.MapWeaponsOfOrderBattle();

if (app.Environment.IsDevelopment())
{
    app.MapDevelopmentAccountNotifications();
}

// Unmatched /api routes stay 404s instead of falling through to the SPA document.
app.MapFallback("/api/{**rest}", () => Results.NotFound());

// Single public origin: deployed environments serve the built React client from wwwroot,
// which is absent during development. See ClientHosting.
app.MapWeaponsOfOrderClient();

// Browser V1 is one App Service instance with one SQLite file on its own persistent
// storage, so the schema travels with the code and is applied here. Off unless an
// environment asks for it, and a failure is allowed to stop the process rather than leave it
// answering requests against a schema that is not there. See DatabaseOptions: a real
// PostgreSQL production environment goes back to an explicit migration step outside the
// application, because two instances starting together would both migrate one database.
await app.Services.MigrateWeaponsOfOrderDatabaseAsync();

app.Run();

/// <summary>Exposed so the API test project can host the real pipeline.</summary>
public partial class Program;
