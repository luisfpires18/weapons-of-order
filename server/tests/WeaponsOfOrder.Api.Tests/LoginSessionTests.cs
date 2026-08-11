using System.Net;
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
        var email = TestAccounts.NewEmail("login-success");
        await client.SignInAsNewAccountAsync(factory, email, TestAccounts.ValidPassword, Cancellation);

        var session = await client.GetSessionAsync(Cancellation);

        Assert.True(session.Authenticated);
        Assert.Equal(email, session.Email);
        Assert.True(session.EmailConfirmed);
        Assert.NotNull(session.AccountId);
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
        var response = await inspector.PostAsync("/api/auth/login", new { email, password }, Cancellation);

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
            new { email, password = "definitely-not-the-password" },
            Cancellation);
        var wrongPasswordBody = await TestAccounts.ReadComparableBodyAsync(wrongPassword, Cancellation);

        var unknownAccount = await stranger.PostAsync(
            "/api/auth/login",
            new { email = TestAccounts.NewEmail("login-nobody"), password = "definitely-not-the-password" },
            Cancellation);
        var unknownAccountBody = await TestAccounts.ReadComparableBodyAsync(unknownAccount, Cancellation);

        Assert.Equal(HttpStatusCode.Unauthorized, wrongPassword.StatusCode);
        Assert.Equal(wrongPassword.StatusCode, unknownAccount.StatusCode);
        Assert.Equal(wrongPasswordBody, unknownAccountBody);

        var failedSession = await stranger.GetSessionAsync(Cancellation);
        Assert.False(failedSession.Authenticated);
    }

    [Fact]
    public async Task An_unconfirmed_account_cannot_sign_in()
    {
        using var client = factory.CreateAuthClient();
        var email = TestAccounts.NewEmail("login-unconfirmed");

        await client.PostAsync(
            "/api/auth/register",
            new { email, password = TestAccounts.ValidPassword },
            Cancellation);

        var response = await client.PostAsync(
            "/api/auth/login",
            new { email, password = TestAccounts.ValidPassword },
            Cancellation);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal("email_not_confirmed", await TestAccounts.ReadProblemCodeAsync(response, Cancellation));

        var session = await client.GetSessionAsync(Cancellation);
        Assert.False(session.Authenticated);
    }

    [Fact]
    public async Task A_wrong_password_on_an_unconfirmed_account_reveals_nothing_about_confirmation()
    {
        using var client = factory.CreateAuthClient();
        var email = TestAccounts.NewEmail("login-unconfirmed-wrong");

        await client.PostAsync(
            "/api/auth/register",
            new { email, password = TestAccounts.ValidPassword },
            Cancellation);

        var response = await client.PostAsync(
            "/api/auth/login",
            new { email, password = "definitely-not-the-password" },
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
