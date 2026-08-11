namespace WeaponsOfOrder.Api.Auth;

/// <summary>
/// Security thresholds for the account flows, bound from the <c>Auth</c> configuration
/// section. AUTH_SECURITY.md treats exact limits as tunable configuration rather than
/// architecture, so they live here instead of being written into the code.
/// </summary>
internal sealed class AuthOptions
{
    public const string SectionName = "Auth";

    /// <summary>
    /// Whether a confirmed email address is required before a session can be established.
    /// </summary>
    /// <remarks>
    /// Enforced by the login endpoint <em>after</em> the password check rather than through
    /// <c>IdentityOptions.SignIn.RequireConfirmedAccount</c>. Identity's own check runs
    /// before the password is verified, which turns any login attempt into an
    /// account-existence oracle. Checking afterwards keeps the requirement while only
    /// disclosing it to a caller who has already proven they know the password.
    /// </remarks>
    public bool RequireConfirmedEmailForSignIn { get; set; } = true;

    /// <summary>
    /// Absolute base URL of the browser client, used to build confirmation and reset links.
    /// </summary>
    /// <remarks>
    /// When empty the link is derived from the incoming request, which is only safe where
    /// the host header is trusted. Deployed environments must set this explicitly.
    /// </remarks>
    public string? ClientBaseUrl { get; set; }

    public PasswordPolicySettings Password { get; set; } = new();

    public LockoutSettings Lockout { get; set; } = new();

    public SessionCookieSettings Cookie { get; set; } = new();

    public RateLimitSettings RateLimit { get; set; } = new();

    public DevelopmentSettings Development { get; set; } = new();
}

/// <summary>
/// Length-first password policy. Composition rules are deliberately off: they push people
/// towards predictable substitutions without adding meaningful entropy.
/// </summary>
internal sealed class PasswordPolicySettings
{
    public int RequiredLength { get; set; } = 12;

    public int RequiredUniqueChars { get; set; } = 4;

    public bool RequireDigit { get; set; }

    public bool RequireLowercase { get; set; }

    public bool RequireUppercase { get; set; }

    public bool RequireNonAlphanumeric { get; set; }
}

/// <summary>Per-account brute-force protection, applied by Identity on failed passwords.</summary>
internal sealed class LockoutSettings
{
    public int MaxFailedAccessAttempts { get; set; } = 5;

    public int LockoutMinutes { get; set; } = 15;
}

internal sealed class SessionCookieSettings
{
    public string Name { get; set; } = "woo.session";

    public int LifetimeDays { get; set; } = 14;
}

/// <summary>Per-caller request budgets, applied before credentials are ever examined.</summary>
internal sealed class RateLimitSettings
{
    public RateLimitWindowSettings Login { get; set; } = new() { PermitLimit = 10, WindowMinutes = 5 };

    public RateLimitWindowSettings Registration { get; set; } = new() { PermitLimit = 5, WindowMinutes = 15 };

    /// <summary>Password-reset requests and confirmation resends.</summary>
    public RateLimitWindowSettings Sensitive { get; set; } = new() { PermitLimit = 5, WindowMinutes = 15 };
}

internal sealed class RateLimitWindowSettings
{
    public int PermitLimit { get; set; } = 10;

    public int WindowMinutes { get; set; } = 5;
}

internal sealed class DevelopmentSettings
{
    /// <summary>
    /// Exposes captured confirmation/reset links through a development-only endpoint.
    /// Ignored outside the Development environment: the endpoint is never mapped there.
    /// </summary>
    public bool ExposeNotifications { get; set; } = true;
}
