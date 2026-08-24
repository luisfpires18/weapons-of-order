using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Options;

namespace WeaponsOfOrder.Api.Hosting;

/// <summary>
/// Reverse-proxy handling for deployed environments.
/// </summary>
/// <remarks>
/// <para>
/// Behind Azure App Service the process sees a plain HTTP request from a platform front
/// end, not the browser's HTTPS request. Without this, <c>Request.IsHttps</c> is false, no
/// <c>Strict-Transport-Security</c> header is written, and every rate-limit partition keys
/// on the front end's address instead of the caller's — turning a per-caller budget into
/// one shared budget the whole internet can exhaust.
/// </para>
/// <para>
/// The middleware is wired here rather than through the
/// <c>ASPNETCORE_FORWARDEDHEADERS_ENABLED</c> application setting so the position in the
/// pipeline and the forward limit are this repository's decision and can be tested.
/// </para>
/// </remarks>
internal static class ForwardedHeadersConfiguration
{
    public static IServiceCollection AddWeaponsOfOrderHosting(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddOptions<HostingOptions>()
            .Bind(configuration.GetSection(HostingOptions.SectionName))
            .ValidateOnStart();

        services.Configure<ForwardedHeadersOptions>(options =>
        {
            // Host is deliberately absent. Account links are built from the configured
            // Auth:ClientBaseUrl and nothing else, so rewriting Request.Host would only
            // widen what a spoofed header can reach without making any link correct.
            options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;

            // The middleware reads these headers right to left, and App Service appends
            // the real caller to whatever X-Forwarded-For the client sent. A limit of one
            // therefore reads the entry the platform wrote and ignores everything a client
            // prepended: `X-Forwarded-For: 1.2.3.4` arrives as `1.2.3.4, <real caller>` and
            // the spoofed value is never used.
            options.ForwardLimit = 1;

            // App Service front-end addresses are not fixed and are not published, so
            // there is no list to pin. The defaults only trust loopback, which no request
            // arrives from here; leaving them in place would silently drop every header.
            // The forward limit above, not an address list, is what contains spoofing.
            options.KnownIPNetworks.Clear();
            options.KnownProxies.Clear();
        });

        return services;
    }

    /// <summary>
    /// Adds the middleware when a deployment has declared it sits behind a trusted proxy.
    /// Must run before anything that reads the scheme or the caller's address.
    /// </summary>
    public static WebApplication UseWeaponsOfOrderForwardedHeaders(this WebApplication app)
    {
        var options = app.Services.GetRequiredService<IOptions<HostingOptions>>().Value;

        if (options.UseForwardedHeaders)
        {
            app.UseForwardedHeaders();
        }

        return app;
    }
}
