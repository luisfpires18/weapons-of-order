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

    /// <summary>
    /// The Identity error codes this endpoint interprets. Kept separate rather than grouped:
    /// a taken username is reported to the player, while a taken address deliberately is not.
    /// </summary>
    private const string DuplicateUserNameCode = "DuplicateUserName";
    private const string DuplicateEmailCode = "DuplicateEmail";
    private const string InvalidUserNameCode = "InvalidUserName";
    private const string InvalidEmailCode = "InvalidEmail";

    /// <summary>
    /// Identity's own column width for <c>NormalizedUserName</c>. A storage limit rather than
    /// a product rule, and the only length the username has.
    /// </summary>
    private const int MaximumUsernameLength = 256;

    private const string UsernameTakenMessage = "That username is already in use.";

    private const string InvalidUsernameMessage =
        "A username cannot contain @, because sign-in reads anything with an @ as an email address.";

    private const string InvalidEmailMessage = "Enter a valid email address.";

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
                new SessionAccount(
                    user.Id,
                    user.UserName ?? string.Empty,
                    user.Email ?? string.Empty,
                    user.EmailConfirmed),
                csrfToken));
    }

    private static async Task<IResult> RegisterAsync(
        RegisterRequest request,
        UserManager<WeaponsOfOrderUser> users,
        IAccountNotificationSender notifications,
        AccountLinkFactory links,
        ILoggerFactory loggerFactory,
        CancellationToken cancellationToken)
    {
        var username = request.Username?.Trim() ?? string.Empty;
        var email = Normalize(request.Email);
        var password = request.Password ?? string.Empty;
        // The username Identity already stores, not a second column: NormalizedUserName and
        // its unique index exist, and the account's Guid Id remains what owns game data.
        var candidate = new WeaponsOfOrderUser { UserName = username, Email = email };
        var errors = new Dictionary<string, string[]>();

        if (ValidateUsername(username) is { } usernameError)
        {
            errors["username"] = [usernameError];
        }

        if (!LooksLikeEmail(email))
        {
            errors["email"] = [InvalidEmailMessage];
        }

        // The password is checked before the account is created so a rejected password is
        // reported as a rejected password, whether or not the address is already in use.
        // That keeps the duplicate-address path below indistinguishable from success.
        var passwordErrors = await ValidatePasswordAsync(users, candidate, password);
        if (passwordErrors.Length > 0)
        {
            errors["password"] = passwordErrors;
        }

        // Every field is reported at once: fixing one only to be told about the next is a
        // round trip for something the server already knew.
        if (errors.Count > 0)
        {
            return AuthProblems.Fields(errors);
        }

        // Asked early so a taken name is answered as a taken name rather than disappearing
        // into the deliberately vague duplicate-address acknowledgement below. It is not what
        // makes the name unique — two simultaneous registrations both pass this — which is
        // why the CreateAsync result is interpreted for the same case.
        if (await users.FindByNameAsync(username) is not null)
        {
            return AuthProblems.Field("username", UsernameTakenMessage);
        }

        var result = await users.CreateAsync(candidate, password);
        if (!result.Succeeded)
        {
            var codes = result.Errors.Select(error => error.Code).ToArray();

            // A username is not a secret: it is chosen to be seen, and saying it is taken is
            // the only way the player can pick another. It discloses nothing about an account
            // either — a taken name says nothing about the address submitted alongside it.
            if (codes.Contains(DuplicateUserNameCode))
            {
                return AuthProblems.Field("username", UsernameTakenMessage);
            }

            if (codes.Contains(InvalidUserNameCode))
            {
                return AuthProblems.Field("username", InvalidUsernameMessage);
            }

            if (codes.Contains(InvalidEmailCode))
            {
                return AuthProblems.Field("email", InvalidEmailMessage);
            }

            if (!codes.All(code => code == DuplicateEmailCode))
            {
                // Codes only. Identity's descriptions can quote the submitted address.
                loggerFactory.CreateLogger(LoggerCategory).LogWarning(
                    "Registration rejected by Identity: {Codes}",
                    string.Join(", ", codes));

                return AuthProblems.RegistrationFailed();
            }

            // The address already has an account. Saying so would let anyone test which
            // addresses are registered, so the caller gets the success response and the
            // existing account is left untouched.
            return Acknowledged();
        }

        await SendEmailConfirmationAsync(
            candidate, users, notifications, links, loggerFactory, cancellationToken);

        return Acknowledged();
    }

    private static async Task<IResult> LoginAsync(
        LoginRequest request,
        UserManager<WeaponsOfOrderUser> users,
        SignInManager<WeaponsOfOrderUser> signIn,
        IOptions<AuthOptions> options)
    {
        var identifier = request.Identifier?.Trim() ?? string.Empty;
        var password = request.Password ?? string.Empty;

        if (identifier.Length == 0 || password.Length == 0)
        {
            return AuthProblems.InvalidCredentials();
        }

        // One field, two namespaces, no ambiguity between them: registration refuses a
        // username containing an at sign, so an identifier carrying one can only be an
        // address. Both lookups go through Identity's normalizer, which is what makes either
        // form case-insensitive. Resolving both and comparing passwords is deliberately not
        // done — it would double the hashing work and, on a legacy account whose UserName is
        // its Email, check the same account twice.
        var user = identifier.Contains('@', StringComparison.Ordinal)
            ? await users.FindByEmailAsync(identifier)
            : await users.FindByNameAsync(identifier);

        if (user is null)
        {
            // Hash something anyway. Skipping the work would make unknown accounts answer
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
        UserManager<WeaponsOfOrderUser> users,
        IAccountNotificationSender notifications,
        AccountLinkFactory links,
        ILoggerFactory loggerFactory,
        CancellationToken cancellationToken)
    {
        var email = Normalize(request.Email);
        var user = LooksLikeEmail(email) ? await users.FindByEmailAsync(email) : null;

        if (user is not null)
        {
            var token = await users.GeneratePasswordResetTokenAsync(user);
            var link = links.TryBuild(AccountLinkFactory.ResetPasswordPath, user.Id, token);

            if (link is null)
            {
                LogUnbuildableLink(loggerFactory, AccountNotificationKind.PasswordReset);
            }
            else
            {
                await notifications.SendAsync(
                    new AccountNotification(AccountNotificationKind.PasswordReset, email, link, DateTimeOffset.UtcNow),
                    cancellationToken);
            }
        }

        // Identical whether or not the address is registered.
        return Acknowledged();
    }

    private static async Task<IResult> ResetPasswordAsync(
        ResetPasswordRequest request,
        UserManager<WeaponsOfOrderUser> users)
    {
        if (!Guid.TryParse(request.UserId, out var userId)
            || !AccountLinkFactory.TryDecodeToken(request.Token, out var token))
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
            || !AccountLinkFactory.TryDecodeToken(request.Token, out var token))
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
        UserManager<WeaponsOfOrderUser> users,
        IAccountNotificationSender notifications,
        AccountLinkFactory links,
        ILoggerFactory loggerFactory,
        CancellationToken cancellationToken)
    {
        var email = Normalize(request.Email);
        var user = LooksLikeEmail(email) ? await users.FindByEmailAsync(email) : null;

        if (user is { EmailConfirmed: false })
        {
            await SendEmailConfirmationAsync(
                user, users, notifications, links, loggerFactory, cancellationToken);
        }

        return Acknowledged();
    }

    private static async Task SendEmailConfirmationAsync(
        WeaponsOfOrderUser user,
        UserManager<WeaponsOfOrderUser> users,
        IAccountNotificationSender notifications,
        AccountLinkFactory links,
        ILoggerFactory loggerFactory,
        CancellationToken cancellationToken)
    {
        var token = await users.GenerateEmailConfirmationTokenAsync(user);
        var link = links.TryBuild(AccountLinkFactory.ConfirmEmailPath, user.Id, token);

        if (link is null)
        {
            LogUnbuildableLink(loggerFactory, AccountNotificationKind.EmailConfirmation);
            return;
        }

        await notifications.SendAsync(
            new AccountNotification(
                AccountNotificationKind.EmailConfirmation,
                user.Email ?? string.Empty,
                link,
                DateTimeOffset.UtcNow),
            cancellationToken);
    }

    /// <summary>
    /// Startup validation makes this unreachable in a running application. It exists so that
    /// if it ever were reached, the caller still returns its ordinary acknowledgement: a
    /// failure visible only for addresses that exist would be an account-existence oracle.
    /// </summary>
    private static void LogUnbuildableLink(ILoggerFactory loggerFactory, AccountNotificationKind kind)
        => loggerFactory.CreateLogger(LoggerCategory).LogError(
            "No trusted client origin is configured, so a {Kind} link was not created. Set "
            + "Auth:ClientBaseUrl.",
            kind);

    /// <summary>
    /// The whole username rule: non-empty, no at sign, and no longer than the column.
    /// </summary>
    /// <remarks>
    /// The at-sign restriction is what makes a single login field possible. An identifier
    /// containing one is an address and an identifier without one is a username, so no input
    /// can ever name two different accounts. Identity's own
    /// <c>AllowedUserNameCharacters</c> whitelist is switched off in
    /// <see cref="AuthServiceCollectionExtensions"/> so this is the only character rule, and
    /// uniqueness is left to Identity and the unique <c>NormalizedUserName</c> index.
    /// </remarks>
    private static string? ValidateUsername(string username) => username switch
    {
        { Length: 0 } => "Choose a username.",
        _ when username.Contains('@', StringComparison.Ordinal) => InvalidUsernameMessage,
        { Length: > MaximumUsernameLength } => $"Use at most {MaximumUsernameLength} characters.",
        _ => null,
    };

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
