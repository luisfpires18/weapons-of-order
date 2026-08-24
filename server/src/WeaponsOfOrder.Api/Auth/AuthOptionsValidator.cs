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
internal sealed class AuthOptionsValidator(IHostEnvironment environment) : IValidateOptions<AuthOptions>
{
    public ValidateOptionsResult Validate(string? name, AuthOptions options)
    {
        if (!AccountLinkFactory.TryParseClientBaseUrl(options.ClientBaseUrl, out var clientBaseUrl))
        {
            return ValidateOptionsResult.Fail(
                $"'{AuthOptions.SectionName}:{nameof(AuthOptions.ClientBaseUrl)}' must be set to the absolute "
                + "origin of the browser client, so confirmation and password-reset links point somewhere "
                + "trusted. Use an https:// URL, or an http:// URL only for a loopback host during local "
                + "development. It must carry no query string or fragment. The origin is never inferred from "
                + "the request, because the Host header is attacker-controlled.");
        }

        // The parser above accepts http on a loopback host so `dotnet run` works. That
        // allowance belongs to Development and nowhere else: a deployed environment
        // pointing account links at http://localhost sends every player a link to their own
        // machine, and one pointing at plain http sends a single-use credential in the
        // clear. Staging is a deployed environment, so it is held to the deployed rule.
        if (!environment.IsDevelopment() && clientBaseUrl.Scheme != Uri.UriSchemeHttps)
        {
            return ValidateOptionsResult.Fail(
                $"'{AuthOptions.SectionName}:{nameof(AuthOptions.ClientBaseUrl)}' must be an https:// origin in the "
                + $"'{environment.EnvironmentName}' environment. Plain http is accepted only for a loopback host "
                + "while running in Development.");
        }

        return ValidateOptionsResult.Success;
    }
}
