using System.Net;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using WeaponsOfOrder.Api.Auth.Notifications;
using WeaponsOfOrder.Infrastructure.Identity;
using Xunit;

namespace WeaponsOfOrder.Api.Tests;

public sealed class EmailConfirmationTests(WeaponsOfOrderApiFactory factory)
    : IClassFixture<WeaponsOfOrderApiFactory>
{
    private CancellationToken Cancellation => TestContext.Current.CancellationToken;

    [Fact]
    public async Task A_confirmation_link_marks_the_address_confirmed()
    {
        using var client = factory.CreateAuthClient();
        var email = TestAccounts.NewEmail("confirm-happy");

        await client.RegisterAsync(
            TestAccounts.UsernameFor(email),
            email,
            TestAccounts.ValidPassword,
            Cancellation);

        var notification = factory.Notifications.Latest(AccountNotificationKind.EmailConfirmation, email);
        Assert.NotNull(notification);
        Assert.StartsWith("https://localhost/confirm-email?", notification.Link, StringComparison.Ordinal);

        var (userId, token) = CapturingNotificationSender.ReadLinkParameters(notification);
        var response = await client.PostAsync("/api/auth/confirm-email", new { userId, token }, Cancellation);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        using var scope = factory.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<WeaponsOfOrderUser>>();
        var stored = await users.FindByEmailAsync(email);

        Assert.NotNull(stored);
        Assert.True(stored.EmailConfirmed);
    }

    [Fact]
    public async Task A_confirmation_token_from_another_account_is_refused()
    {
        using var client = factory.CreateAuthClient();
        var target = TestAccounts.NewEmail("confirm-target");
        var other = TestAccounts.NewEmail("confirm-other");

        await client.RegisterAsync(
            TestAccounts.UsernameFor(target),
            target,
            TestAccounts.ValidPassword,
            Cancellation);
        await client.RegisterAsync(
            TestAccounts.UsernameFor(other),
            other,
            TestAccounts.ValidPassword,
            Cancellation);

        var targetNotification = factory.Notifications.Latest(AccountNotificationKind.EmailConfirmation, target);
        var otherNotification = factory.Notifications.Latest(AccountNotificationKind.EmailConfirmation, other);
        Assert.NotNull(targetNotification);
        Assert.NotNull(otherNotification);

        var (targetId, _) = CapturingNotificationSender.ReadLinkParameters(targetNotification);
        var (_, otherToken) = CapturingNotificationSender.ReadLinkParameters(otherNotification);

        // The id in the link only selects a row. The token is bound to its own account.
        var response = await client.PostAsync(
            "/api/auth/confirm-email",
            new { userId = targetId, token = otherToken },
            Cancellation);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("invalid_token", await TestAccounts.ReadProblemCodeAsync(response, Cancellation));
    }

    [Fact]
    public async Task A_malformed_confirmation_link_is_refused()
    {
        using var client = factory.CreateAuthClient();

        var response = await client.PostAsync(
            "/api/auth/confirm-email",
            new { userId = "not-a-guid", token = "nonsense" },
            Cancellation);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("invalid_token", await TestAccounts.ReadProblemCodeAsync(response, Cancellation));
    }

    [Fact]
    public async Task Resending_answers_the_same_whether_or_not_the_address_is_registered()
    {
        using var client = factory.CreateAuthClient();
        var registered = TestAccounts.NewEmail("resend-known");

        await client.RegisterAsync(
            TestAccounts.UsernameFor(registered),
            registered,
            TestAccounts.ValidPassword,
            Cancellation);

        var known = await client.PostAsync(
            "/api/auth/resend-confirmation",
            new { email = registered },
            Cancellation);
        var knownBody = await TestAccounts.ReadComparableBodyAsync(known, Cancellation);

        var unknown = await client.PostAsync(
            "/api/auth/resend-confirmation",
            new { email = TestAccounts.NewEmail("resend-unknown") },
            Cancellation);
        var unknownBody = await TestAccounts.ReadComparableBodyAsync(unknown, Cancellation);

        Assert.Equal(HttpStatusCode.Accepted, known.StatusCode);
        Assert.Equal(known.StatusCode, unknown.StatusCode);
        Assert.Equal(knownBody, unknownBody);

        // The registered address did get a second link; the stranger's did not.
        Assert.Equal(2, factory.Notifications.Sent.Count(notification =>
            notification.Kind == AccountNotificationKind.EmailConfirmation
            && notification.Email == registered));
    }

    [Fact]
    public async Task Resending_to_an_already_confirmed_account_produces_no_new_link()
    {
        using var client = factory.CreateAuthClient();
        var email = TestAccounts.NewEmail("resend-confirmed");
        await client.SignInAsNewAccountAsync(factory, email, TestAccounts.ValidPassword, Cancellation);

        var before = factory.Notifications.Sent.Count(notification =>
            notification.Kind == AccountNotificationKind.EmailConfirmation && notification.Email == email);

        var response = await client.PostAsync("/api/auth/resend-confirmation", new { email }, Cancellation);

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        Assert.Equal(before, factory.Notifications.Sent.Count(notification =>
            notification.Kind == AccountNotificationKind.EmailConfirmation && notification.Email == email));
    }
}
