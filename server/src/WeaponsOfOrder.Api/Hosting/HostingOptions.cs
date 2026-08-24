namespace WeaponsOfOrder.Api.Hosting;

/// <summary>
/// How this process is exposed to the browser, bound from the <c>Hosting</c> configuration
/// section.
/// </summary>
/// <remarks>
/// AUTH_SECURITY.md leaves forwarded-header handling to deployment on purpose: whether the
/// application is behind a reverse proxy is a property of where it runs, not of the code.
/// These switches are how a deployment states it.
/// </remarks>
internal sealed class HostingOptions
{
    public const string SectionName = "Hosting";

    /// <summary>
    /// Whether a trusted reverse proxy terminates TLS in front of this process, so
    /// <c>X-Forwarded-Proto</c> and <c>X-Forwarded-For</c> should be honoured.
    /// </summary>
    /// <remarks>
    /// Off by default. Local development talks to Kestrel directly, and a process that
    /// trusts these headers while reachable without a proxy lets any caller claim any
    /// address and any scheme. Azure App Service sets this to <c>true</c> through an
    /// application setting; see <see cref="ForwardedHeadersConfiguration"/> for why the
    /// rightmost header entry is the trustworthy one there.
    /// </remarks>
    public bool UseForwardedHeaders { get; set; }
}
