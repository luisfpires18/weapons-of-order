using System.Net;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using WeaponsOfOrder.Infrastructure.Identity;
using Xunit;

namespace WeaponsOfOrder.Api.Tests;

public sealed class LoginSessionTests(WeaponsOfOrderApiFactory factory)
    : IClassFixture<WeaponsOfOrderApiFactory>
{
    private CancellationToken Cancellation => TestContext.Current.CancellationToken;

    [Fact]
    public async Task An_unauthenticated_visitor_gets_a_session_answer_rather_than_an_error()
    {
        using var client = factory.CreateAuthClient();

        var session = await client.GetSessionAsync(Cancellation);

        Assert.False(session.Authenticated);
        Assert.Null(session.AccountId);
        // Still handed an antiforgery token: the login form needs one to submit.
        Assert.NotEmpty(session.CsrfToken);
    }

    [Fact]
    public async Task Valid_credentials_establish_a_session_the_next_request_sees()
    {
        using var client = factory.CreateAuthClient();
        var username = TestAccounts.NewUsername("login-success");
        var email = TestAccounts.NewEmail("login-success");
        await client.SignInAsNewAccountAsync(factory, username, email, TestAccounts.ValidPassword, Cancellation);

        var session = await client.GetSessionAsync(Cancellation);

        Assert.True(session.Authenticated);
        // The player-facing identifier, published beside the address rather than instead of it.
        Assert.Equal(username, session.Username);
        Assert.Equal(email, session.Email);
        Assert.True(session.EmailConfirmed);
        Assert.NotNull(session.AccountId);
    }

    /// <summary>
    /// The session says who the player is; it does not hand out Identity's bookkeeping.
    /// </summary>
    [Fact]
    public async Task The_session_publishes_nothing_beyond_the_four_account_fields()
    {
        using var client = factory.CreateAuthClient();
        var email = TestAccounts.NewEmail("login-session-shape");
        await client.SignInAsNewAccountAsync(factory, email, TestAccounts.ValidPassword, Cancellation);

        var response = await client.Http.GetAsync("/api/auth/session", Cancellation);
        var account = (await TestAccounts.ReadBodyAsync(response, Cancellation)).GetProperty("account");

        Assert.Equal(
            new[] { "email", "emailConfirmed", "id", "username" },
            account.EnumerateObject()
                .Select(property => property.Name)
                .Order(StringComparer.Ordinal)
                .ToArray());
    }

    /// <summary>
    /// One field, either namespace, and the account is the same one whichever was typed —
    /// including the Guid every player-owned row is keyed by.
    /// </summary>
    [Fact]
    public async Task The_same_account_is_reached_by_username_or_by_address_in_any_case()
    {
        using var client = factory.CreateAuthClient();
        var username = TestAccounts.NewUsername("Login-Identifier");
        var email = TestAccounts.NewEmail("login-identifier");

        await client.SignInAsNewAccountAsync(factory, username, email, TestAccounts.ValidPassword, Cancellation);
        var expected = (await client.GetSessionAsync(Cancellation)).AccountId;
        Assert.NotNull(expected);

        string[] identifiers =
        [
            username,
            username.ToLowerInvariant(),
            username.ToUpperInvariant(),
            email,
            email.ToUpperInvariant(),
        ];

        foreach (var identifier in identifiers)
        {
            using var browser = factory.CreateAuthClient();

            var response = await browser.PostAsync(
                "/api/auth/login",
                new { identifier, password = TestAccounts.ValidPassword },
                Cancellation);

            Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

            var session = await browser.GetSessionAsync(Cancellation);
            Assert.True(session.Authenticated);
            Assert.Equal(expected, session.AccountId);
            // What is stored is what the player chose, not the casing they signed in with.
            Assert.Equal(username, session.Username);
        }
    }

    /// <summary>
    /// Surrounding whitespace is what a phone keyboard adds to a pasted name or address. It
    /// is trimmed rather than turned into a failed attempt against the lockout counter.
    /// </summary>
    [Fact]
    public async Task A_padded_identifier_still_resolves()
    {
        using var client = factory.CreateAuthClient();
        var username = TestAccounts.NewUsername("login-padded");
        var email = TestAccounts.NewEmail("login-padded");
        await client.SignInAsNewAccountAsync(factory, username, email, TestAccounts.ValidPassword, Cancellation);

        using var browser = factory.CreateAuthClient();
        var response = await browser.PostAsync(
            "/api/auth/login",
            new { identifier = $"  {username}  ", password = TestAccounts.ValidPassword },
            Cancellation);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    /// <summary>
    /// Accounts created before usernames existed have <c>UserName</c> equal to their address.
    /// They keep working: the address contains an at sign, so the identifier resolves as an
    /// address, and the account's Guid — which owns everything they forged — is untouched.
    /// </summary>
    [Fact]
    public async Task A_legacy_account_whose_name_is_its_address_still_signs_in_by_email()
    {
        var email = TestAccounts.NewEmail("login-legacy");

        Guid legacyId;
        using (var scope = factory.Services.CreateScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<WeaponsOfOrderUser>>();

            // Exactly what the pre-Task-8 registration wrote.
            var legacy = new WeaponsOfOrderUser { UserName = email, Email = email };
            var created = await users.CreateAsync(legacy, TestAccounts.ValidPassword);
            Assert.True(created.Succeeded);

            var token = await users.GenerateEmailConfirmationTokenAsync(legacy);
            Assert.True((await users.ConfirmEmailAsync(legacy, token)).Succeeded);

            legacyId = legacy.Id;
        }

        using var client = factory.CreateAuthClient();
        var response = await client.PostAsync(
            "/api/auth/login",
            new { identifier = email, password = TestAccounts.ValidPassword },
            Cancellation);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        var session = await client.GetSessionAsync(Cancellation);
        Assert.Equal(legacyId, session.AccountId);
        Assert.Equal(email, session.Email);
        // Still its address, and this task deliberately does not rewrite it.
        Assert.Equal(email, session.Username);
    }

    [Fact]
    public async Task The_session_cookie_is_http_only_and_secure()
    {
        using var client = factory.CreateAuthClient();
        var email = TestAccounts.NewEmail("login-cookie");
        var password = TestAccounts.ValidPassword;

        await client.SignInAsNewAccountAsync(factory, email, password, Cancellation);

        // Re-run the login on a fresh client so the Set-Cookie header can be inspected.
        using var inspector = factory.CreateAuthClient();
        var response = await inspector.PostAsync(
            "/api/auth/login",
            new { identifier = email, password },
            Cancellation);

        var setCookie = Assert.Single(
            response.Headers.GetValues("Set-Cookie"),
            value => value.StartsWith("woo.session=", StringComparison.Ordinal));

        Assert.Contains("httponly", setCookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("secure", setCookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("samesite=lax", setCookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("path=/", setCookie, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task An_unknown_address_and_a_wrong_password_are_answered_identically()
    {
        using var client = factory.CreateAuthClient();
        var email = TestAccounts.NewEmail("login-enumeration");
        await client.SignInAsNewAccountAsync(factory, email, TestAccounts.ValidPassword, Cancellation);

        using var stranger = factory.CreateAuthClient();

        var wrongPassword = await stranger.PostAsync(
            "/api/auth/login",
            new { identifier = email, password = "definitely-not-the-password" },
            Cancellation);
        var wrongPasswordBody = await TestAccounts.ReadComparableBodyAsync(wrongPassword, Cancellation);

        var unknownAddress = await stranger.PostAsync(
            "/api/auth/login",
            new
            {
                identifier = TestAccounts.NewEmail("login-nobody"),
                password = "definitely-not-the-password",
            },
            Cancellation);
        var unknownAddressBody = await TestAccounts.ReadComparableBodyAsync(unknownAddress, Cancellation);

        var unknownUsername = await stranger.PostAsync(
            "/api/auth/login",
            new
            {
                identifier = TestAccounts.NewUsername("login-nobody"),
                password = "definitely-not-the-password",
            },
            Cancellation);
        var unknownUsernameBody = await TestAccounts.ReadComparableBodyAsync(unknownUsername, Cancellation);

        Assert.Equal(HttpStatusCode.Unauthorized, wrongPassword.StatusCode);
        Assert.Equal(wrongPassword.StatusCode, unknownAddress.StatusCode);
        Assert.Equal(wrongPassword.StatusCode, unknownUsername.StatusCode);
        Assert.Equal(wrongPasswordBody, unknownAddressBody);
        // A name nobody holds is answered exactly as a wrong password is, so the login form
        // cannot be used to find out which usernames exist either.
        Assert.Equal(wrongPasswordBody, unknownUsernameBody);

        var failedSession = await stranger.GetSessionAsync(Cancellation);
        Assert.False(failedSession.Authenticated);
    }

    [Fact]
    public async Task An_unconfirmed_account_cannot_sign_in()
    {
        using var client = factory.CreateAuthClient();
        var email = TestAccounts.NewEmail("login-unconfirmed");
        var username = TestAccounts.NewUsername("login-unconfirmed");

        await client.RegisterAsync(username, email, TestAccounts.ValidPassword, Cancellation);

        // Both identifiers reach the same rule, and both reach it only after the password.
        foreach (var identifier in new[] { username, email })
        {
            var response = await client.PostAsync(
                "/api/auth/login",
                new { identifier, password = TestAccounts.ValidPassword },
                Cancellation);

            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
            Assert.Equal("email_not_confirmed", await TestAccounts.ReadProblemCodeAsync(response, Cancellation));
        }

        var session = await client.GetSessionAsync(Cancellation);
        Assert.False(session.Authenticated);
    }

    [Fact]
    public async Task A_wrong_password_on_an_unconfirmed_account_reveals_nothing_about_confirmation()
    {
        using var client = factory.CreateAuthClient();
        var email = TestAccounts.NewEmail("login-unconfirmed-wrong");

        await client.RegisterAsync(
            TestAccounts.UsernameFor(email),
            email,
            TestAccounts.ValidPassword,
            Cancellation);

        var response = await client.PostAsync(
            "/api/auth/login",
            new { identifier = email, password = "definitely-not-the-password" },
            Cancellation);

        // The confirmation state is only disclosed once the password has been proven.
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("invalid_credentials", await TestAccounts.ReadProblemCodeAsync(response, Cancellation));
    }

    [Fact]
    public async Task Logout_ends_the_server_session()
    {
        using var client = factory.CreateAuthClient();
        var email = TestAccounts.NewEmail("logout");
        await client.SignInAsNewAccountAsync(factory, email, TestAccounts.ValidPassword, Cancellation);

        var response = await client.PostAsync("/api/auth/logout", new { }, Cancellation);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        var session = await client.GetSessionAsync(Cancellation);
        Assert.False(session.Authenticated);
        Assert.Null(session.AccountId);
    }

    [Fact]
    public async Task A_protected_endpoint_rejects_a_caller_without_a_session()
    {
        using var client = factory.CreateAuthClient();

        var response = await client.PostAsync("/api/auth/logout", new { }, Cancellation);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("unauthenticated", await TestAccounts.ReadProblemCodeAsync(response, Cancellation));
    }

    [Fact]
    public async Task A_session_cannot_be_claimed_by_submitting_someone_elses_account_id()
    {
        using var client = factory.CreateAuthClient();
        var victimEmail = TestAccounts.NewEmail("login-victim");
        await client.SignInAsNewAccountAsync(factory, victimEmail, TestAccounts.ValidPassword, Cancellation);

        var victim = await client.GetSessionAsync(Cancellation);
        Assert.NotNull(victim.AccountId);

        // A different browser posting the victim's id gets nothing: identity comes from the
        // cookie the server issued, never from the request.
        using var attacker = factory.CreateAuthClient();
        var response = await attacker.PostAsync(
            "/api/auth/logout",
            new { userId = victim.AccountId },
            Cancellation);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);

        var victimSessionAfter = await client.GetSessionAsync(Cancellation);
        Assert.True(victimSessionAfter.Authenticated);
    }
}
