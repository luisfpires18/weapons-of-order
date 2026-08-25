using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace WeaponsOfOrder.Api.Tests;

/// <summary>What <c>GET /api/auth/session</c> tells the browser.</summary>
public sealed record SessionSnapshot(
    bool Authenticated,
    Guid? AccountId,
    string? Username,
    string? Email,
    bool EmailConfirmed,
    string CsrfToken);

/// <summary>
/// Drives the account API the way the browser client does: read the session to obtain an
/// antiforgery request token, then send it back in the agreed header on every mutation.
/// </summary>
public sealed class AuthTestClient(HttpClient http) : IDisposable
{
    public const string AntiforgeryHeaderName = "X-WoO-Antiforgery";

    public HttpClient Http { get; } = http;

    public async Task<SessionSnapshot> GetSessionAsync(CancellationToken cancellationToken)
    {
        var response = await Http.GetAsync("/api/auth/session", cancellationToken);
        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
        var authenticated = body.GetProperty("authenticated").GetBoolean();
        var account = body.GetProperty("account");

        return new SessionSnapshot(
            authenticated,
            account.ValueKind == JsonValueKind.Object ? account.GetProperty("id").GetGuid() : null,
            account.ValueKind == JsonValueKind.Object ? account.GetProperty("username").GetString() : null,
            account.ValueKind == JsonValueKind.Object ? account.GetProperty("email").GetString() : null,
            account.ValueKind == JsonValueKind.Object && account.GetProperty("emailConfirmed").GetBoolean(),
            body.GetProperty("csrfToken").GetString() ?? string.Empty);
    }

    /// <summary>Posts with a freshly read antiforgery token, as the client always does.</summary>
    public async Task<HttpResponseMessage> PostAsync(string path, object body, CancellationToken cancellationToken)
    {
        var session = await GetSessionAsync(cancellationToken);
        return await PostAsync(path, body, session.CsrfToken, cancellationToken);
    }

    public Task<HttpResponseMessage> PostAsync(
        string path,
        object body,
        string? csrfToken,
        CancellationToken cancellationToken)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, path) { Content = JsonContent.Create(body) };

        if (csrfToken is not null)
        {
            request.Headers.Add(AntiforgeryHeaderName, csrfToken);
        }

        return Http.SendAsync(request, cancellationToken);
    }

    /// <summary>
    /// Registers, confirms and signs in, for tests whose subject is something else.
    /// </summary>
    /// <remarks>
    /// The username is derived from the address rather than asked for, because most callers
    /// only need an account that exists. Tests about the name itself pass one explicitly.
    /// </remarks>
    public Task<string> SignInAsNewAccountAsync(
        WeaponsOfOrderApiFactory factory,
        string email,
        string password,
        CancellationToken cancellationToken)
        => SignInAsNewAccountAsync(factory, TestAccounts.UsernameFor(email), email, password, cancellationToken);

    /// <inheritdoc cref="SignInAsNewAccountAsync(WeaponsOfOrderApiFactory, string, string, CancellationToken)"/>
    public async Task<string> SignInAsNewAccountAsync(
        WeaponsOfOrderApiFactory factory,
        string username,
        string email,
        string password,
        CancellationToken cancellationToken)
    {
        await RegisterAsync(username, email, password, cancellationToken);
        await ConfirmAsync(factory, email, cancellationToken);
        await SignInAsync(email, password, cancellationToken);

        return email;
    }

    /// <summary>Registers an account and asserts the acknowledgement, nothing more.</summary>
    public async Task RegisterAsync(
        string username,
        string email,
        string password,
        CancellationToken cancellationToken)
    {
        var registration = await PostAsync(
            "/api/auth/register",
            new { username, email, password },
            cancellationToken);

        Assert.Equal(System.Net.HttpStatusCode.Accepted, registration.StatusCode);
    }

    /// <summary>Follows the confirmation link that registration produced.</summary>
    public async Task ConfirmAsync(
        WeaponsOfOrderApiFactory factory,
        string email,
        CancellationToken cancellationToken)
    {
        var confirmation = factory.Notifications.Latest(
            WeaponsOfOrder.Api.Auth.Notifications.AccountNotificationKind.EmailConfirmation,
            email);
        Assert.NotNull(confirmation);

        var (userId, token) = CapturingNotificationSender.ReadLinkParameters(confirmation);
        var confirmed = await PostAsync("/api/auth/confirm-email", new { userId, token }, cancellationToken);
        Assert.Equal(System.Net.HttpStatusCode.NoContent, confirmed.StatusCode);
    }

    /// <summary>
    /// Signs in an account that already exists, for tests that need a second browser holding
    /// the same account — which is how a reload or a new device is proven to see the same
    /// server-side state.
    /// </summary>
    /// <param name="identifier">A username or an email address, as the login field accepts.</param>
    public async Task SignInAsync(string identifier, string password, CancellationToken cancellationToken)
    {
        var login = await PostAsync(
            "/api/auth/login",
            new { identifier, password },
            cancellationToken);

        Assert.Equal(System.Net.HttpStatusCode.NoContent, login.StatusCode);
    }

    public void Dispose() => Http.Dispose();
}
