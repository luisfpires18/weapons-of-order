namespace WeaponsOfOrder.Api.Auth;

/// <summary>
/// The error vocabulary of the account API.
/// </summary>
/// <remarks>
/// Every response is <c>application/problem+json</c> with a stable <c>code</c> extension
/// the client switches on, so UI copy never has to parse prose. Details are written for a
/// player: no Identity error codes, no stack traces, and nothing that distinguishes
/// "no such account" from "wrong password".
/// </remarks>
internal static class AuthProblems
{
    public const string InvalidCredentialsCode = "invalid_credentials";
    public const string EmailNotConfirmedCode = "email_not_confirmed";
    public const string InvalidTokenCode = "invalid_token";
    public const string ValidationCode = "validation";
    public const string RegistrationFailedCode = "registration_failed";
    public const string UnauthenticatedCode = "unauthenticated";
    public const string RateLimitedCode = "rate_limited";

    /// <summary>
    /// The single answer to an unknown username, an unknown address, a wrong password, and a
    /// locked-out account. Naming the lockout would confirm the account is registered.
    /// </summary>
    public static IResult InvalidCredentials() => Problem(
        StatusCodes.Status401Unauthorized,
        InvalidCredentialsCode,
        "Sign-in failed.",
        "That sign-in and password combination is not correct. Repeated failures lock the account "
        + "for a while.");

    /// <summary>
    /// Safe to state plainly: the caller reaches this only after a successful password check.
    /// </summary>
    public static IResult EmailNotConfirmed() => Problem(
        StatusCodes.Status403Forbidden,
        EmailNotConfirmedCode,
        "Email not confirmed.",
        "Confirm your email address before entering the world.");

    public static IResult InvalidToken(string detail) => Problem(
        StatusCodes.Status400BadRequest,
        InvalidTokenCode,
        "Link is not usable.",
        detail);

    public static IResult RegistrationFailed() => Problem(
        StatusCodes.Status400BadRequest,
        RegistrationFailedCode,
        "Registration failed.",
        "The account could not be created. Try again.");

    public static IResult Unauthenticated() => Problem(
        StatusCodes.Status401Unauthorized,
        UnauthenticatedCode,
        "Not signed in.",
        "Sign in to continue.");

    public static IResult Fields(IDictionary<string, string[]> errors) => Results.ValidationProblem(
        errors,
        statusCode: StatusCodes.Status400BadRequest,
        title: "Some details need fixing.",
        extensions: new Dictionary<string, object?> { ["code"] = ValidationCode });

    public static IResult Field(string field, params string[] messages)
        => Fields(new Dictionary<string, string[]> { [field] = messages });

    private static IResult Problem(int statusCode, string code, string title, string detail) => Results.Problem(
        title: title,
        detail: detail,
        statusCode: statusCode,
        extensions: new Dictionary<string, object?> { ["code"] = code });
}
