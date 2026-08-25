using System.Net;
using Xunit;

namespace WeaponsOfOrder.Api.Tests;

/// <summary>
/// Cookie authentication means the browser attaches the session to cross-site requests too,
/// so every mutation carries a token that a cross-site page cannot obtain.
/// </summary>
public sealed class AntiforgeryTests(WeaponsOfOrderApiFactory factory)
    : IClassFixture<WeaponsOfOrderApiFactory>
{
    private CancellationToken Cancellation => TestContext.Current.CancellationToken;

    [Fact]
    public async Task A_mutation_without_a_token_is_rejected()
    {
        using var client = factory.CreateAuthClient();
        var email = TestAccounts.NewEmail("antiforgery-missing");
        await client.SignInAsNewAccountAsync(factory, email, TestAccounts.ValidPassword, Cancellation);

        var response = await client.PostAsync("/api/auth/logout", new { }, csrfToken: null, Cancellation);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("antiforgery", await TestAccounts.ReadProblemCodeAsync(response, Cancellation));

        // Rejected, not partially applied: the session is still there.
        var session = await client.GetSessionAsync(Cancellation);
        Assert.True(session.Authenticated);
    }

    [Fact]
    public async Task A_mutation_with_a_forged_token_is_rejected()
    {
        using var client = factory.CreateAuthClient();
        var email = TestAccounts.NewEmail("antiforgery-forged");
        await client.SignInAsNewAccountAsync(factory, email, TestAccounts.ValidPassword, Cancellation);

        var response = await client.PostAsync(
            "/api/auth/logout",
            new { },
            "CfDJ8-this-is-not-a-real-antiforgery-token",
            Cancellation);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("antiforgery", await TestAccounts.ReadProblemCodeAsync(response, Cancellation));
    }

    [Fact]
    public async Task A_token_issued_to_a_different_browser_is_rejected()
    {
        using var holder = factory.CreateAuthClient();
        var email = TestAccounts.NewEmail("antiforgery-crossclient");
        await holder.SignInAsNewAccountAsync(factory, email, TestAccounts.ValidPassword, Cancellation);

        // A well-formed token is not enough. It has to match the antiforgery cookie held by
        // the browser that is sending it, which is what a cross-site page cannot arrange.
        using var other = factory.CreateAuthClient();
        var stolenToken = (await other.GetSessionAsync(Cancellation)).CsrfToken;

        var response = await holder.PostAsync("/api/auth/logout", new { }, stolenToken, Cancellation);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("antiforgery", await TestAccounts.ReadProblemCodeAsync(response, Cancellation));
    }

    [Fact]
    public async Task A_token_minted_before_sign_in_no_longer_matches_the_new_identity()
    {
        using var client = factory.CreateAuthClient();
        var anonymousToken = (await client.GetSessionAsync(Cancellation)).CsrfToken;

        var email = TestAccounts.NewEmail("antiforgery-stale");
        await client.SignInAsNewAccountAsync(factory, email, TestAccounts.ValidPassword, Cancellation);

        var response = await client.PostAsync("/api/auth/logout", new { }, anonymousToken, Cancellation);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("antiforgery", await TestAccounts.ReadProblemCodeAsync(response, Cancellation));
    }

    [Fact]
    public async Task A_mutation_with_the_matching_token_succeeds()
    {
        using var client = factory.CreateAuthClient();
        var email = TestAccounts.NewEmail("antiforgery-valid");
        await client.SignInAsNewAccountAsync(factory, email, TestAccounts.ValidPassword, Cancellation);

        var token = (await client.GetSessionAsync(Cancellation)).CsrfToken;
        var response = await client.PostAsync("/api/auth/logout", new { }, token, Cancellation);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    public async Task Unauthenticated_mutations_are_protected_too()
    {
        using var client = factory.CreateAuthClient();

        var response = await client.PostAsync(
            "/api/auth/login",
            new
            {
                identifier = TestAccounts.NewEmail("antiforgery-login"),
                password = TestAccounts.ValidPassword,
            },
            csrfToken: null,
            Cancellation);

        // Login CSRF — forcing a victim into an attacker-controlled session — is a real
        // attack, so the rule has no exemption for anonymous callers.
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("antiforgery", await TestAccounts.ReadProblemCodeAsync(response, Cancellation));
    }

    [Fact]
    public async Task Reading_the_session_needs_no_token()
    {
        using var client = factory.CreateAuthClient();

        var response = await client.Http.GetAsync("/api/auth/session", Cancellation);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
