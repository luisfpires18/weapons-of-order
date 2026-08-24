using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Extensions.FileProviders;
using Microsoft.Net.Http.Headers;

namespace WeaponsOfOrder.Api.Hosting;

/// <summary>
/// Serves the built React client from the same origin as <c>/api</c>.
/// </summary>
/// <remarks>
/// <para>
/// TECH_STACK.md fixes one public origin for Browser V1. A deployment publishes the Vite
/// build into <c>wwwroot</c> beside the application; local development has no such
/// directory, because Vite serves the client on its own port and proxies <c>/api</c> back
/// here, which keeps the browser on one origin there too. So the whole static pipeline is
/// only wired up when the directory actually exists.
/// </para>
/// <para>
/// Direct navigation to a client route such as <c>/battle</c> is a request App Service
/// knows nothing about; the fallback below is what turns it into the React document
/// instead of a 404.
/// </para>
/// </remarks>
internal static class ClientHosting
{
    /// <summary>Vite writes every hashed build asset here.</summary>
    private const string HashedAssetPath = "/assets";

    /// <summary>
    /// One year, which is what <c>immutable</c> is worth saying for. Safe only because the
    /// filename carries a content hash: a changed file is a different URL.
    /// </summary>
    private const string ImmutableCacheControl = "public, max-age=31536000, immutable";

    /// <summary>
    /// Revalidate every time. Applied to the files that keep their name across releases.
    /// </summary>
    private const string RevalidateCacheControl = "no-cache, must-revalidate";

    public static WebApplication MapWeaponsOfOrderClient(this WebApplication app)
    {
        var webRoot = Path.Combine(app.Environment.ContentRootPath, "wwwroot");

        if (!Directory.Exists(webRoot))
        {
            return app;
        }

        var contentTypes = new FileExtensionContentTypeProvider();
        // Spelled out rather than assumed: the PWA is not installable if the manifest is
        // served as anything else, and that failure is invisible until an install is tried.
        contentTypes.Mappings[".webmanifest"] = "application/manifest+json";

        var staticFiles = new StaticFileOptions
        {
            FileProvider = new PhysicalFileProvider(webRoot),
            ContentTypeProvider = contentTypes,
            OnPrepareResponse = SetCacheHeaders,
        };

        app.UseDefaultFiles(new DefaultFilesOptions { FileProvider = staticFiles.FileProvider });
        app.UseStaticFiles(staticFiles);

        app.MapFallbackToFile("index.html", staticFiles);

        return app;
    }

    /// <summary>
    /// Long-lived caching for content-hashed assets, and none at all for the files that
    /// keep their name across releases.
    /// </summary>
    /// <remarks>
    /// <c>index.html</c>, the service worker and the manifest are the entry points to a
    /// specific build. Cached, a browser would keep loading the previous release's asset
    /// graph after a deployment — and the service worker would keep serving it offline,
    /// which is a stale-shell bug no redeploy can clear.
    /// </remarks>
    private static void SetCacheHeaders(StaticFileResponseContext context)
    {
        var hashed = context.Context.Request.Path
            .StartsWithSegments(HashedAssetPath, StringComparison.OrdinalIgnoreCase);

        context.Context.Response.Headers[HeaderNames.CacheControl] =
            hashed ? ImmutableCacheControl : RevalidateCacheControl;
    }
}
