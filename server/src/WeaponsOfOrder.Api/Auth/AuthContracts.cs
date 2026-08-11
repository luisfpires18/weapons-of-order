namespace WeaponsOfOrder.Api.Auth;

internal sealed record RegisterRequest(string? Email, string? Password);

internal sealed record LoginRequest(string? Email, string? Password, bool RememberMe = false);

internal sealed record ForgotPasswordRequest(string? Email);

internal sealed record ResendConfirmationRequest(string? Email);

/// <remarks>
/// <paramref name="UserId"/> comes from the emailed link and only selects which record to
/// check. It is not an authorization claim: <paramref name="Token"/> is bound by Identity's
/// data protector to that specific account and purpose, so a mismatched id fails.
/// </remarks>
internal sealed record ResetPasswordRequest(string? UserId, string? Token, string? Password);

/// <inheritdoc cref="ResetPasswordRequest"/>
internal sealed record ConfirmEmailRequest(string? UserId, string? Token);

/// <summary>
/// What the browser is allowed to know about the current session.
/// </summary>
/// <remarks>
/// <paramref name="CsrfToken"/> is the antiforgery request token for the identity this
/// response was produced under. Identity changes (login, logout) invalidate it, which is
/// why the client re-reads the session after either.
/// </remarks>
internal sealed record SessionResponse(bool Authenticated, SessionAccount? Account, string CsrfToken);

/// <summary>
/// Only the fields the UI actually needs. Security stamps, hashes, lockout counters and
/// the rest of Identity's bookkeeping stay on the server.
/// </summary>
internal sealed record SessionAccount(Guid Id, string Email, bool EmailConfirmed);

/// <summary>
/// The deliberately identical answer to "register" and "resend confirmation" whether or not
/// the address belongs to an account.
/// </summary>
internal sealed record AcknowledgedResponse(string Status);
