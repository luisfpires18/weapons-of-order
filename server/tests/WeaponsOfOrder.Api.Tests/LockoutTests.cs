using System.Net;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using WeaponsOfOrder.Infrastructure.Identity;
using Xunit;

namespace WeaponsOfOrder.Api.Tests;

public sealed class LockoutTests(WeaponsOfOrderApiFactory factory)
    : IClassFixture<WeaponsOfOrderApiFactory>
{
    private CancellationToken Cancellation => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Repeated_wrong_passwords_lock_the_account_out()
    {
        using var scope = factory.Services.CreateScope();
        var identity = scope.ServiceProvider.GetRequiredService<IOptions<IdentityOptions>>().Value;
        var maxAttempts = identity.Lockout.MaxFailedAccessAttempts;

        using var client = factory.CreateAuthClient();
        var email = TestAccounts.NewEmail("lockout");
        await client.SignInAsNewAccountAsync(factory, email, TestAccounts.ValidPassword, Cancellation);
        await client.PostAsync("/api/auth/logout", new { }, Cancellation);

        for (var attempt = 0; attempt < maxAttempts; attempt++)
        {
            var failure = await client.PostAsync(
                "/api/auth/login",
                new { email, password = $"wrong-password-{attempt}" },
                Cancellation);

            Assert.Equal(HttpStatusCode.Unauthorized, failure.StatusCode);
        }

        var withCorrectPassword = await client.PostAsync(
            "/api/auth/login",
            new { email, password = TestAccounts.ValidPassword },
            Cancellation);

        // Now even the right password fails, and it fails with the same generic answer:
        // naming the lockout would confirm the address is registered.
        Assert.Equal(HttpStatusCode.Unauthorized, withCorrectPassword.StatusCode);
        Assert.Equal("invalid_credentials", await TestAccounts.ReadProblemCodeAsync(withCorrectPassword, Cancellation));

        var users = scope.ServiceProvider.GetRequiredService<UserManager<WeaponsOfOrderUser>>();
        var stored = await users.FindByEmailAsync(email);
        Assert.NotNull(stored);
        Assert.True(await users.IsLockedOutAsync(stored));

        var session = await client.GetSessionAsync(Cancellation);
        Assert.False(session.Authenticated);
    }

    [Fact]
    public async Task A_successful_sign_in_clears_the_failure_count()
    {
        using var client = factory.CreateAuthClient();
        var email = TestAccounts.NewEmail("lockout-reset");
        await client.SignInAsNewAccountAsync(factory, email, TestAccounts.ValidPassword, Cancellation);
        await client.PostAsync("/api/auth/logout", new { }, Cancellation);

        await client.PostAsync("/api/auth/login", new { email, password = "wrong-once" }, Cancellation);

        var success = await client.PostAsync(
            "/api/auth/login",
            new { email, password = TestAccounts.ValidPassword },
            Cancellation);
        Assert.Equal(HttpStatusCode.NoContent, success.StatusCode);

        using var scope = factory.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<WeaponsOfOrderUser>>();
        var stored = await users.FindByEmailAsync(email);

        Assert.NotNull(stored);
        Assert.Equal(0, await users.GetAccessFailedCountAsync(stored));
    }
}
