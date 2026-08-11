using System.Text;
using Microsoft.AspNetCore.WebUtilities;

namespace WeaponsOfOrder.Api.Auth.Notifications;

/// <summary>
/// Builds the client URLs that carry Identity's confirmation and reset tokens.
/// </summary>
internal static class AccountLinks
{
    public const string ConfirmEmailPath = "/confirm-email";

    public const string ResetPasswordPath = "/reset-password";

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

    public static string Build(HttpRequest request, AuthOptions options, string path, Guid userId, string token)
    {
        // A configured base URL is preferred: deriving one from the request means trusting
        // the Host header, which an attacker can set unless the host is pinned upstream.
        var baseUrl = string.IsNullOrWhiteSpace(options.ClientBaseUrl)
            ? $"{request.Scheme}://{request.Host}"
            : options.ClientBaseUrl.TrimEnd('/');

        var query = QueryString.Create(new Dictionary<string, string?>
        {
            ["userId"] = userId.ToString(),
            ["token"] = EncodeToken(token),
        });

        return $"{baseUrl}{path}{query}";
    }
}
