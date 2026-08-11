using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;
using WeaponsOfOrder.Api.Auth.Notifications;
using WeaponsOfOrder.Api.Security;
using WeaponsOfOrder.Infrastructure.Identity;

namespace WeaponsOfOrder.Api.Auth;

/// <summary>
/// The account and session API the browser client talks to.
/// </summary>
internal static class AuthEndpoints
{
    private const string RoutePrefix = "/api/auth";

    private const string LoggerCategory = "WeaponsOfOrder.Api.Auth";

    /// <summary>Identity's codes for "this address already has an account".</summary>
    private static readonly string[] DuplicateErrorCodes = ["DuplicateUserName", "DuplicateEmail"];

    /// <summary>Identity's codes for an address it will not accept at all.</summary>
    private static readonly string[] InvalidEmailErrorCodes = ["InvalidEmail", "InvalidUserName"];

    public static IEndpointRouteBuilder MapWeaponsOfOrderAuth(this IEndpointRouteBuilder endpoints)
    {
        // Reading the session is a safe method: no antiforgery token is required to call
        // it, which is what lets the client bootstrap one.
        endpoints.MapGet($"{RoutePrefix}/session", GetSessionAsync);

        var mutations = endpoints.MapMutations(RoutePrefix);

        mutations.MapPost("/register", RegisterAsync).RequireRateLimiting(AuthRateLimits.Registration);
        mutations.MapPost("/login", LoginAsync).RequireRateLimiting(AuthRateLimits.Login);
        mutations.MapPost("/forgot-password", ForgotPasswordAsync).RequireRateLimiting(AuthRateLimits.Sensitive);
        mutations.MapPost("/reset-password", ResetPasswordAsync).RequireRateLimiting(AuthRateLimits.Sensitive);
        mutations.MapPost("/confirm-email", ConfirmEmailAsync).RequireRateLimiting(AuthRateLimits.Sensitive);
        mutations.MapPost("/resend-confirmation", ResendConfirmationAsync).RequireRateLimiting(AuthRateLimits.Sensitive);

        // The one mutation that needs an established session. It is also the pattern
        // gameplay endpoints will copy: authorization from the server session, antiforgery
        // from the group.
        mutations.MapPost("/logout", LogoutAsync).RequireAuthorization();

        return endpoints;
    }

    /// <summary>
    /// Reports whether this browser has a session and hands back the antiforgery request
    /// token bound to the resulting identity.
    /// </summary>
    /// <remarks>
    /// Answers 200 in both states. An unauthenticated visitor asking who they are is a
    /// normal question, not an error, and the client needs the token either way to submit
    /// the login form.
    /// </remarks>
    private static async Task<IResult> GetSessionAsync(
        HttpContext http,
        IAntiforgery antiforgery,
        UserManager<WeaponsOfOrderUser> users)
    {
        var csrfToken = antiforgery.GetAndStoreTokens(http).RequestToken ?? string.Empty;

        // The cookie proves who the caller claims to be; the account row is what the
        // answer is built from, so a deleted account stops looking signed in immediately.
        var user = http.User.Identity?.IsAuthenticated == true
            ? await users.GetUserAsync(http.User)
            : null;

        return user is null
            ? Results.Ok(new SessionResponse(false, null, csrfToken))
            : Results.Ok(new SessionResponse(
                true,
                new SessionAccount(user.Id, user.Email ?? string.Empty, user.EmailConfirmed),
                csrfToken));
    }

    private static async Task<IResult> RegisterAsync(
        RegisterRequest request,
        HttpContext http,
        UserManager<WeaponsOfOrderUser> users,
        IAccountNotificationSender notifications,
        IOptions<AuthOptions> options,
        ILoggerFactory loggerFactory,
        CancellationToken cancellationToken)
    {
        var email = Normalize(request.Email);
        var password = request.Password ?? string.Empty;
        var candidate = new WeaponsOfOrderUser { UserName = email, Email = email };
        var errors = new Dictionary<string, string[]>();

        if (!LooksLikeEmail(email))
        {
            errors["email"] = ["Enter a valid email address."];
        }

        // The password is checked before the account is created so a rejected password is
        // reported as a rejected password, whether or not the address is already in use.
        // That keeps the duplicate path below indistinguishable from success.
        var passwordErrors = await ValidatePasswordAsync(users, candidate, password);
        if (passwordErrors.Length > 0)
        {
            errors["password"] = passwordErrors;
        }

        // Both fields are reported at once: fixing one only to be told about the other is a
        // second round trip for something the server already knew.
        if (errors.Count > 0)
        {
            return AuthProblems.Fields(errors);
        }

        var result = await users.CreateAsync(candidate, password);
        if (!result.Succeeded)
        {
            if (result.Errors.Any(error => InvalidEmailErrorCodes.Contains(error.Code)))
            {
                return AuthProblems.Field("email", "Enter a valid email address.");
            }

            if (!result.Errors.All(error => DuplicateErrorCodes.Contains(error.Code)))
            {
                // Codes only. Identity's descriptions can quote the submitted address.
                loggerFactory.CreateLogger(LoggerCategory).LogWarning(
                    "Registration rejected by Identity: {Codes}",
                    string.Join(", ", result.Errors.Select(error => error.Code)));

                return AuthProblems.RegistrationFailed();
            }

            // The address already has an account. Saying so would let anyone test which
            // addresses are registered, so the caller gets the success response and the
            // existing account is left untouched.
            return Acknowledged();
        }

        await SendEmailConfirmationAsync(candidate, http, users, notifications, options.Value, cancellationToken);

        return Acknowledged();
    }

    private static async Task<IResult> LoginAsync(
        LoginRequest request,
        UserManager<WeaponsOfOrderUser> users,
        SignInManager<WeaponsOfOrderUser> signIn,
        IOptions<AuthOptions> options)
    {
        var email = Normalize(request.Email);
        var password = request.Password ?? string.Empty;

        if (email.Length == 0 || password.Length == 0)
        {
            return AuthProblems.InvalidCredentials();
        }

        var user = await users.FindByEmailAsync(email);
        if (user is null)
        {
            // Hash something anyway. Skipping the work would make unknown addresses answer
            // measurably faster than known ones.
            users.PasswordHasher.HashPassword(new WeaponsOfOrderUser(), password);
            return AuthProblems.InvalidCredentials();
        }

        // Checks the password and drives the lockout counter, but — unlike
        // PasswordSignInAsync — does not reject unconfirmed accounts before looking at the
        // password. Confirmation is enforced below, once the caller has proven themselves.
        var result = await signIn.CheckPasswordSignInAsync(user, password, lockoutOnFailure: true);
        if (!result.Succeeded)
        {
            return AuthProblems.InvalidCredentials();
        }

        if (options.Value.RequireConfirmedEmailForSignIn && !user.EmailConfirmed)
        {
            return AuthProblems.EmailNotConfirmed();
        }

        await signIn.SignInAsync(user, isPersistent: request.RememberMe);

        // No body: the client re-reads the session, which is also how it picks up an
        // antiforgery token bound to its new identity.
        return Results.NoContent();
    }

    private static async Task<IResult> LogoutAsync(SignInManager<WeaponsOfOrderUser> signIn)
    {
        await signIn.SignOutAsync();
        return Results.NoContent();
    }

    private static async Task<IResult> ForgotPasswordAsync(
        ForgotPasswordRequest request,
        HttpContext http,
        UserManager<WeaponsOfOrderUser> users,
        IAccountNotificationSender notifications,
        IOptions<AuthOptions> options,
        CancellationToken cancellationToken)
    {
        var email = Normalize(request.Email);
        var user = LooksLikeEmail(email) ? await users.FindByEmailAsync(email) : null;

        if (user is not null)
        {
            var token = await users.GeneratePasswordResetTokenAsync(user);
            var link = AccountLinks.Build(http.Request, options.Value, AccountLinks.ResetPasswordPath, user.Id, token);

            await notifications.SendAsync(
                new AccountNotification(AccountNotificationKind.PasswordReset, email, link, DateTimeOffset.UtcNow),
                cancellationToken);
        }

        // Identical whether or not the address is registered.
        return Acknowledged();
    }

    private static async Task<IResult> ResetPasswordAsync(
        ResetPasswordRequest request,
        UserManager<WeaponsOfOrderUser> users)
    {
        if (!Guid.TryParse(request.UserId, out var userId)
            || !AccountLinks.TryDecodeToken(request.Token, out var token))
        {
            return InvalidResetLink();
        }

        var user = await users.FindByIdAsync(userId.ToString());
        if (user is null)
        {
            return InvalidResetLink();
        }

        var password = request.Password ?? string.Empty;
        var passwordErrors = await ValidatePasswordAsync(users, user, password);
        if (passwordErrors.Length > 0)
        {
            return AuthProblems.Field("password", passwordErrors);
        }

        // The token, not the id in the request, is what authorizes this: Identity binds it
        // to this account, this purpose and this security stamp, and consumes it on use.
        var result = await users.ResetPasswordAsync(user, token, password);
        if (!result.Succeeded)
        {
            return result.Errors.Any(error => error.Code == "InvalidToken")
                ? InvalidResetLink()
                : AuthProblems.Field("password", [.. result.Errors.Select(error => error.Description)]);
        }

        // Resetting rolls the security stamp, so any session opened with the old password
        // is dropped the next time its cookie is validated. The caller signs in again
        // rather than being handed a session by a flow that only proved mailbox access.
        return Results.NoContent();
    }

    private static async Task<IResult> ConfirmEmailAsync(
        ConfirmEmailRequest request,
        UserManager<WeaponsOfOrderUser> users)
    {
        if (!Guid.TryParse(request.UserId, out var userId)
            || !AccountLinks.TryDecodeToken(request.Token, out var token))
        {
            return InvalidConfirmationLink();
        }

        var user = await users.FindByIdAsync(userId.ToString());
        if (user is null)
        {
            return InvalidConfirmationLink();
        }

        var result = await users.ConfirmEmailAsync(user, token);

        return result.Succeeded ? Results.NoContent() : InvalidConfirmationLink();
    }

    private static async Task<IResult> ResendConfirmationAsync(
        ResendConfirmationRequest request,
        HttpContext http,
        UserManager<WeaponsOfOrderUser> users,
        IAccountNotificationSender notifications,
        IOptions<AuthOptions> options,
        CancellationToken cancellationToken)
    {
        var email = Normalize(request.Email);
        var user = LooksLikeEmail(email) ? await users.FindByEmailAsync(email) : null;

        if (user is { EmailConfirmed: false })
        {
            await SendEmailConfirmationAsync(user, http, users, notifications, options.Value, cancellationToken);
        }

        return Acknowledged();
    }

    private static async Task SendEmailConfirmationAsync(
        WeaponsOfOrderUser user,
        HttpContext http,
        UserManager<WeaponsOfOrderUser> users,
        IAccountNotificationSender notifications,
        AuthOptions options,
        CancellationToken cancellationToken)
    {
        var token = await users.GenerateEmailConfirmationTokenAsync(user);
        var link = AccountLinks.Build(http.Request, options, AccountLinks.ConfirmEmailPath, user.Id, token);

        await notifications.SendAsync(
            new AccountNotification(
                AccountNotificationKind.EmailConfirmation,
                user.Email ?? string.Empty,
                link,
                DateTimeOffset.UtcNow),
            cancellationToken);
    }

    private static async Task<string[]> ValidatePasswordAsync(
        UserManager<WeaponsOfOrderUser> users,
        WeaponsOfOrderUser user,
        string password)
    {
        if (password.Length == 0)
        {
            return ["Enter a password."];
        }

        var messages = new List<string>();
        foreach (var validator in users.PasswordValidators)
        {
            var result = await validator.ValidateAsync(users, user, password);
            if (!result.Succeeded)
            {
                messages.AddRange(result.Errors.Select(error => error.Description));
            }
        }

        return [.. messages];
    }

    /// <summary>Lower-cased and trimmed so "A@b.com" and "a@b.com " are the same account.</summary>
    private static string Normalize(string? email) => email?.Trim().ToLowerInvariant() ?? string.Empty;

    private static bool LooksLikeEmail(string email)
        => email.Length is > 0 and <= 254 && new EmailAddressAttribute().IsValid(email);

    private static IResult Acknowledged() => Results.Accepted(value: new AcknowledgedResponse("acknowledged"));

    private static IResult InvalidResetLink() => AuthProblems.InvalidToken(
        "This password reset link has expired or has already been used. Request a new one.");

    private static IResult InvalidConfirmationLink() => AuthProblems.InvalidToken(
        "This confirmation link has expired or has already been used. Request a new one.");
}
