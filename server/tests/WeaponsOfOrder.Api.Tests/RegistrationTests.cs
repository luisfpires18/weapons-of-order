using System.Net;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using WeaponsOfOrder.Api.Auth.Notifications;
using WeaponsOfOrder.Infrastructure.Identity;
using Xunit;

namespace WeaponsOfOrder.Api.Tests;

public sealed class RegistrationTests(WeaponsOfOrderApiFactory factory)
    : IClassFixture<WeaponsOfOrderApiFactory>
{
    private CancellationToken Cancellation => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Valid_registration_persists_an_account_and_sends_a_confirmation()
    {
        using var client = factory.CreateAuthClient();
        var email = TestAccounts.NewEmail("register-valid");

        var response = await client.PostAsync(
            "/api/auth/register",
            new { email, password = TestAccounts.ValidPassword },
            Cancellation);

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);

        using var scope = factory.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<WeaponsOfOrderUser>>();
        var stored = await users.FindByEmailAsync(email);

        Assert.NotNull(stored);
        Assert.False(stored.EmailConfirmed);
        // The password is stored as an Identity hash, never as anything reversible.
        Assert.NotNull(stored.PasswordHash);
        Assert.DoesNotContain(TestAccounts.ValidPassword, stored.PasswordHash, StringComparison.Ordinal);

        Assert.NotNull(factory.Notifications.Latest(AccountNotificationKind.EmailConfirmation, email));
    }

    [Fact]
    public async Task Registration_normalizes_the_submitted_address()
    {
        using var client = factory.CreateAuthClient();
        var email = TestAccounts.NewEmail("register-case");

        var response = await client.PostAsync(
            "/api/auth/register",
            new { email = $"  {email.ToUpperInvariant()}  ", password = TestAccounts.ValidPassword },
            Cancellation);

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);

        using var scope = factory.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<WeaponsOfOrderUser>>();

        Assert.NotNull(await users.FindByEmailAsync(email));
    }

    [Theory]
    [InlineData("not-an-email")]
    [InlineData("")]
    [InlineData("missing@")]
    public async Task Malformed_addresses_are_rejected_as_field_errors(string email)
    {
        using var client = factory.CreateAuthClient();

        var response = await client.PostAsync(
            "/api/auth/register",
            new { email, password = TestAccounts.ValidPassword },
            Cancellation);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("validation", await TestAccounts.ReadProblemCodeAsync(response, Cancellation));

        var body = await TestAccounts.ReadBodyAsync(response, Cancellation);
        Assert.True(body.GetProperty("errors").TryGetProperty("email", out _));
    }

    [Fact]
    public async Task A_bad_address_and_a_bad_password_are_reported_together()
    {
        using var client = factory.CreateAuthClient();

        var response = await client.PostAsync(
            "/api/auth/register",
            new { email = "not-an-email", password = "short" },
            Cancellation);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var errors = (await TestAccounts.ReadBodyAsync(response, Cancellation)).GetProperty("errors");

        Assert.True(errors.TryGetProperty("email", out _));
        Assert.True(errors.TryGetProperty("password", out _));
    }

    [Fact]
    public async Task A_password_below_the_policy_is_rejected_without_creating_an_account()
    {
        using var client = factory.CreateAuthClient();
        var email = TestAccounts.NewEmail("register-weak");

        var response = await client.PostAsync(
            "/api/auth/register",
            new { email, password = "short" },
            Cancellation);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var body = await TestAccounts.ReadBodyAsync(response, Cancellation);
        Assert.True(body.GetProperty("errors").TryGetProperty("password", out _));

        using var scope = factory.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<WeaponsOfOrderUser>>();

        Assert.Null(await users.FindByEmailAsync(email));
    }

    [Fact]
    public async Task Registering_a_taken_address_answers_exactly_as_a_new_one_does()
    {
        using var client = factory.CreateAuthClient();
        var email = TestAccounts.NewEmail("register-duplicate");

        var first = await client.PostAsync(
            "/api/auth/register",
            new { email, password = TestAccounts.ValidPassword },
            Cancellation);
        var firstBody = await TestAccounts.ReadComparableBodyAsync(first, Cancellation);

        var second = await client.PostAsync(
            "/api/auth/register",
            new { email, password = "a-completely-different-password-9" },
            Cancellation);
        var secondBody = await TestAccounts.ReadComparableBodyAsync(second, Cancellation);

        // Status and body are identical, so the response cannot be used to test which
        // addresses are registered.
        Assert.Equal(first.StatusCode, second.StatusCode);
        Assert.Equal(firstBody, secondBody);

        using var scope = factory.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<WeaponsOfOrderUser>>();
        var stored = await users.FindByEmailAsync(email);

        Assert.NotNull(stored);
        // The second attempt must not have overwritten the first account's password.
        Assert.Equal(
            PasswordVerificationResult.Success,
            users.PasswordHasher.VerifyHashedPassword(stored, stored.PasswordHash!, TestAccounts.ValidPassword));
    }

    [Fact]
    public async Task A_weak_password_for_a_taken_address_still_reports_only_the_password()
    {
        using var client = factory.CreateAuthClient();
        var email = TestAccounts.NewEmail("register-duplicate-weak");

        await client.PostAsync(
            "/api/auth/register",
            new { email, password = TestAccounts.ValidPassword },
            Cancellation);

        var response = await client.PostAsync("/api/auth/register", new { email, password = "short" }, Cancellation);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var body = await TestAccounts.ReadBodyAsync(response, Cancellation);
        var errors = body.GetProperty("errors");

        Assert.True(errors.TryGetProperty("password", out _));
        Assert.False(errors.TryGetProperty("email", out _));
    }
}
