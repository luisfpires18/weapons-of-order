using Microsoft.Extensions.Options;
using WeaponsOfOrder.Api.Auth.Notifications;

namespace WeaponsOfOrder.Api.Auth;

/// <summary>
/// Refuses to start the application when the account flows are configured in a way that
/// cannot work safely.
/// </summary>
/// <remarks>
/// Checked at startup rather than when the first confirmation link is needed. Discovering
/// this on a live registration would mean either a broken account nobody can confirm, or a
/// 500 that only appears for addresses that exist — an account-existence oracle. Refusing
/// to boot is the failure a deployment can actually see and fix.
/// </remarks>
internal sealed class AuthOptionsValidator : IValidateOptions<AuthOptions>
{
    public ValidateOptionsResult Validate(string? name, AuthOptions options)
        => AccountLinkFactory.TryParseClientBaseUrl(options.ClientBaseUrl, out _)
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(
                $"'{AuthOptions.SectionName}:{nameof(AuthOptions.ClientBaseUrl)}' must be set to the absolute "
                + "origin of the browser client, so confirmation and password-reset links point somewhere "
                + "trusted. Use an https:// URL, or an http:// URL only for a loopback host during local "
                + "development. It must carry no query string or fragment. The origin is never inferred from "
                + "the request, because the Host header is attacker-controlled.");
}
