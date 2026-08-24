using System.Globalization;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using WeaponsOfOrder.Api.Auth.Notifications;
using WeaponsOfOrder.Api.Security;
using WeaponsOfOrder.Infrastructure;
using WeaponsOfOrder.Infrastructure.Identity;

namespace WeaponsOfOrder.Api.Auth;

/// <summary>
/// Composition seam for accounts and sessions: Identity, the browser cookie, antiforgery
/// and the credential-endpoint rate limits.
/// </summary>
internal static class AuthServiceCollectionExtensions
{
    public static IServiceCollection AddWeaponsOfOrderAuth(
        this IServiceCollection services,
        IConfiguration configuration,
        IWebHostEnvironment environment)
    {
        services.AddOptions<AuthOptions>()
            .Bind(configuration.GetSection(AuthOptions.SectionName))
            // ValidateOnStart, so a deployment with no trusted client origin fails at boot
            // rather than on somebody's first password reset.
            .ValidateOnStart();

        services.AddSingleton<IValidateOptions<AuthOptions>>(new AuthOptionsValidator(environment));
        services.AddSingleton<AccountLinkFactory>();

        AddIdentity(services);
        AddSessionCookie(services, environment);
        AddAntiforgery(services, environment);
        AddRateLimiting(services);
        AddNotificationDelivery(services, configuration, environment);

        return services;
    }

    private static void AddIdentity(IServiceCollection services)
    {
        // Core rather than AddIdentity: the role stores would be dead schema until an
        // actual admin feature exists, and AUTH_SECURITY.md defers that model.
        services.AddIdentityCore<WeaponsOfOrderUser>()
            .AddWeaponsOfOrderIdentityStores()
            .AddSignInManager()
            .AddDefaultTokenProviders();

        // AddIdentityCore does not register these, but the identity cookies below validate
        // the security stamp on every request and resolve them from the container.
        // Without them a password reset could not invalidate a live session.
        services.TryAddScoped<ISecurityStampValidator, SecurityStampValidator<WeaponsOfOrderUser>>();
        services.TryAddScoped<ITwoFactorSecurityStampValidator, TwoFactorSecurityStampValidator<WeaponsOfOrderUser>>();

        services.AddOptions<SecurityStampValidatorOptions>().Configure(options =>
            // Default is 30 minutes. A password reset should push existing sessions out
            // sooner than that; the cost is one extra account read per session per window.
            options.ValidationInterval = TimeSpan.FromMinutes(5));

        services.AddOptions<IdentityOptions>().Configure<IOptions<AuthOptions>>((identity, auth) =>
        {
            var settings = auth.Value;

            identity.User.RequireUniqueEmail = true;

            identity.Password.RequiredLength = settings.Password.RequiredLength;
            identity.Password.RequiredUniqueChars = settings.Password.RequiredUniqueChars;
            identity.Password.RequireDigit = settings.Password.RequireDigit;
            identity.Password.RequireLowercase = settings.Password.RequireLowercase;
            identity.Password.RequireUppercase = settings.Password.RequireUppercase;
            identity.Password.RequireNonAlphanumeric = settings.Password.RequireNonAlphanumeric;

            identity.Lockout.AllowedForNewUsers = true;
            identity.Lockout.MaxFailedAccessAttempts = settings.Lockout.MaxFailedAccessAttempts;
            identity.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(settings.Lockout.LockoutMinutes);

            // Left false on purpose. See AuthOptions.RequireConfirmedEmailForSignIn: the
            // login endpoint enforces confirmation after the password check so the answer
            // cannot be used to discover which addresses are registered.
            identity.SignIn.RequireConfirmedAccount = false;
            identity.SignIn.RequireConfirmedEmail = false;
        });
    }

    private static void AddSessionCookie(IServiceCollection services, IWebHostEnvironment environment)
    {
        services.AddAuthentication(IdentityConstants.ApplicationScheme).AddIdentityCookies();
        services.AddAuthorization();

        services.AddOptions<CookieAuthenticationOptions>(IdentityConstants.ApplicationScheme)
            .Configure<IOptions<AuthOptions>>((cookie, auth) =>
            {
                var settings = auth.Value.Cookie;

                cookie.Cookie.Name = settings.Name;
                cookie.Cookie.Path = "/";
                // Not readable from JavaScript, so an XSS bug cannot exfiltrate the session.
                cookie.Cookie.HttpOnly = true;
                // Lax, not Strict: the client and API share one origin, so no legitimate
                // request is cross-site, but Lax still lets a plain link into the game
                // arrive already signed in. It blocks the cross-site POST that antiforgery
                // also guards, giving two independent layers.
                cookie.Cookie.SameSite = SameSiteMode.Lax;
                cookie.Cookie.SecurePolicy = environment.IsDevelopment()
                    // Local development runs over plain http on localhost.
                    ? CookieSecurePolicy.SameAsRequest
                    : CookieSecurePolicy.Always;
                // Authentication is not optional functionality, so it is exempt from
                // consent-based cookie policies rather than silently dropped.
                cookie.Cookie.IsEssential = true;

                cookie.ExpireTimeSpan = TimeSpan.FromDays(settings.LifetimeDays);
                cookie.SlidingExpiration = true;

                // This is a JSON API. Redirecting an unauthenticated fetch to an HTML login
                // page would hand the client a 200 containing markup instead of an error.
                cookie.Events.OnRedirectToLogin = context => WriteStatus(context, AuthProblems.Unauthenticated());
                cookie.Events.OnRedirectToAccessDenied = context => WriteStatus(
                    context,
                    Results.Problem(
                        title: "Not allowed.",
                        statusCode: StatusCodes.Status403Forbidden,
                        extensions: new Dictionary<string, object?> { ["code"] = "forbidden" }));
            });

        static Task WriteStatus(RedirectContext<CookieAuthenticationOptions> context, IResult result)
            => result.ExecuteAsync(context.HttpContext);
    }

    private static void AddAntiforgery(IServiceCollection services, IWebHostEnvironment environment)
        => services.AddAntiforgery(options =>
        {
            options.HeaderName = ApiSecurity.AntiforgeryHeaderName;
            options.Cookie.Name = "woo.antiforgery";
            // The cookie half of the token pair stays unreadable to scripts. The client
            // gets the request half from the session response body instead, so a
            // cross-site page — which cannot read that response — cannot forge the pair.
            options.Cookie.HttpOnly = true;
            options.Cookie.SameSite = SameSiteMode.Lax;
            options.Cookie.SecurePolicy = environment.IsDevelopment()
                ? CookieSecurePolicy.SameAsRequest
                : CookieSecurePolicy.Always;
            options.Cookie.IsEssential = true;
        });

    private static void AddRateLimiting(IServiceCollection services)
        => services.AddRateLimiter(limiter =>
        {
            limiter.AddPolicy(AuthRateLimits.Login, Window(AuthRateLimits.Login, o => o.RateLimit.Login));
            limiter.AddPolicy(
                AuthRateLimits.Registration,
                Window(AuthRateLimits.Registration, o => o.RateLimit.Registration));
            limiter.AddPolicy(AuthRateLimits.Sensitive, Window(AuthRateLimits.Sensitive, o => o.RateLimit.Sensitive));

            limiter.OnRejected = async (context, cancellationToken) =>
            {
                if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
                {
                    context.HttpContext.Response.Headers.RetryAfter =
                        ((int)retryAfter.TotalSeconds).ToString(CultureInfo.InvariantCulture);
                }

                await Results.Problem(
                        title: "Too many attempts.",
                        detail: "Wait a moment before trying again.",
                        statusCode: StatusCodes.Status429TooManyRequests,
                        extensions: new Dictionary<string, object?> { ["code"] = AuthProblems.RateLimitedCode })
                    .ExecuteAsync(context.HttpContext);
            };
        });

    /// <summary>
    /// A fixed window per caller address. Limits are read per partition from configuration
    /// so a deployment can tighten them without a code change.
    /// </summary>
    private static Func<HttpContext, RateLimitPartition<string>> Window(
        string policyName,
        Func<AuthOptions, RateLimitWindowSettings> select)
        => context =>
        {
            var options = context.RequestServices.GetRequiredService<IOptionsMonitor<AuthOptions>>().CurrentValue;
            var settings = select(options);

            // Unauthenticated flows have no account to key on, so the caller address is the
            // only partition available. Behind a proxy this needs forwarded headers to be
            // configured, which is deployment work.
            var caller = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";

            return RateLimitPartition.GetFixedWindowLimiter(
                $"{policyName}|{caller}",
                _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = Math.Max(1, settings.PermitLimit),
                    Window = TimeSpan.FromMinutes(Math.Max(1, settings.WindowMinutes)),
                    QueueLimit = 0,
                });
        };

    private static void AddNotificationDelivery(
        IServiceCollection services,
        IConfiguration configuration,
        IWebHostEnvironment environment)
    {
        services.AddOptions<EmailOptions>()
            .Bind(configuration.GetSection(EmailOptions.SectionName))
            .ValidateOnStart();

        services.AddSingleton<IValidateOptions<EmailOptions>, EmailOptionsValidator>();

        if (environment.IsDevelopment())
        {
            services.AddSingleton<DevelopmentNotificationOutbox>();
            services.AddSingleton<IAccountNotificationSender, DevelopmentAccountNotificationSender>();
            return;
        }

        // Resolved from IOptions rather than read straight off IConfiguration here, so the
        // choice honours every configuration source the host ends up with rather than only
        // the ones present at composition time.
        services.AddSingleton<IAccountNotificationSender>(provider => AccountNotificationDelivery.Create(
            provider,
            provider.GetRequiredService<IOptions<EmailOptions>>().Value));
    }
}

internal static class AuthRateLimits
{
    public const string Login = "auth-login";
    public const string Registration = "auth-registration";
    public const string Sensitive = "auth-sensitive";
}
