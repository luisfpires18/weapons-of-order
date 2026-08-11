using System.Diagnostics.CodeAnalysis;
using System.Text;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Options;

namespace WeaponsOfOrder.Api.Auth.Notifications;

/// <summary>
/// Builds the client URLs that carry Identity's confirmation and reset tokens.
/// </summary>
/// <remarks>
/// Deliberately has no access to the incoming request. An earlier version fell back to
/// <c>request.Scheme</c> and <c>request.Host</c> when no base URL was configured, which
/// meant an attacker who could set the <c>Host</c> header could have a reset link addressed
/// to their own domain mailed to somebody else. The only source of the origin is
/// <c>Auth:ClientBaseUrl</c>, validated at startup by <see cref="AuthOptionsValidator"/>.
/// </remarks>
internal sealed class AccountLinkFactory
{
    public const string ConfirmEmailPath = "/confirm-email";

    public const string ResetPasswordPath = "/reset-password";

    private readonly Uri? _clientBaseUrl;

    public AccountLinkFactory(IOptions<AuthOptions> options)
        => _clientBaseUrl = TryParseClientBaseUrl(options.Value.ClientBaseUrl, out var parsed) ? parsed : null;

    /// <summary>
    /// Whether an origin is configured. False means no account link can be produced; the
    /// caller must carry on and answer normally rather than surfacing the misconfiguration,
    /// which would otherwise distinguish a registered address from an unknown one.
    /// </summary>
    [MemberNotNullWhen(true, nameof(_clientBaseUrl))]
    public bool IsConfigured => _clientBaseUrl is not null;

    /// <summary>
    /// Accepts an absolute HTTPS origin, or an HTTP one only when the host is loopback so
    /// local development works. A relative value, a foreign scheme such as
    /// <c>javascript:</c>, or a URL carrying a query or fragment is rejected.
    /// </summary>
    public static bool TryParseClientBaseUrl(string? value, [NotNullWhen(true)] out Uri? parsed)
    {
        parsed = null;

        if (string.IsNullOrWhiteSpace(value)
            || !Uri.TryCreate(value.Trim().TrimEnd('/'), UriKind.Absolute, out var candidate))
        {
            return false;
        }

        if (candidate.Scheme != Uri.UriSchemeHttps
            && !(candidate.Scheme == Uri.UriSchemeHttp && candidate.IsLoopback))
        {
            return false;
        }

        if (candidate.Query.Length > 0 || candidate.Fragment.Length > 0)
        {
            return false;
        }

        parsed = candidate;
        return true;
    }

    /// <summary>
    /// Identity tokens are opaque strings that can contain characters a URL round trip
    /// mangles, so they travel base64url-encoded — the same convention the framework's own
    /// account templates use.
    /// </summary>
    public static string EncodeToken(string token)
        => WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(token));

    public static bool TryDecodeToken(string? encoded, out string token)
    {
        token = string.Empty;
        if (string.IsNullOrEmpty(encoded))
        {
            return false;
        }

        try
        {
            token = Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(encoded));
            return true;
        }
        catch (FormatException)
        {
            return false;
        }
    }

    /// <summary>Returns null when no trusted origin is configured.</summary>
    public string? TryBuild(string path, Guid userId, string token)
    {
        if (!IsConfigured)
        {
            return null;
        }

        var query = QueryString.Create(new Dictionary<string, string?>
        {
            ["userId"] = userId.ToString(),
            ["token"] = EncodeToken(token),
        });

        return $"{_clientBaseUrl.GetLeftPart(UriPartial.Path).TrimEnd('/')}{path}{query}";
    }
}
