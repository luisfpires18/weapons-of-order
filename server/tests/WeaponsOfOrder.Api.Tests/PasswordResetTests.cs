using System.Net;
using WeaponsOfOrder.Api.Auth.Notifications;
using Xunit;

namespace WeaponsOfOrder.Api.Tests;

public sealed class PasswordResetTests(WeaponsOfOrderApiFactory factory)
    : IClassFixture<WeaponsOfOrderApiFactory>
{
    private const string ReplacementPassword = "molten-quench-anvil-41";

    private CancellationToken Cancellation => TestContext.Current.CancellationToken;

    [Fact]
    public async Task A_reset_link_replaces_the_password_and_retires_the_old_one()
    {
        using var client = factory.CreateAuthClient();
        var email = TestAccounts.NewEmail("reset-happy");
        await client.SignInAsNewAccountAsync(factory, email, TestAccounts.ValidPassword, Cancellation);
        await client.PostAsync("/api/auth/logout", new { }, Cancellation);

        var requested = await client.PostAsync("/api/auth/forgot-password", new { email }, Cancellation);
        Assert.Equal(HttpStatusCode.Accepted, requested.StatusCode);

        var notification = factory.Notifications.Latest(AccountNotificationKind.PasswordReset, email);
        Assert.NotNull(notification);
        Assert.StartsWith("https://localhost/reset-password?", notification.Link, StringComparison.Ordinal);

        var (userId, token) = CapturingNotificationSender.ReadLinkParameters(notification);
        var reset = await client.PostAsync(
            "/api/auth/reset-password",
            new { userId, token, password = ReplacementPassword },
            Cancellation);

        Assert.Equal(HttpStatusCode.NoContent, reset.StatusCode);

        var withOldPassword = await client.PostAsync(
            "/api/auth/login",
            new { email, password = TestAccounts.ValidPassword },
            Cancellation);
        Assert.Equal(HttpStatusCode.Unauthorized, withOldPassword.StatusCode);

        var withNewPassword = await client.PostAsync(
            "/api/auth/login",
            new { email, password = ReplacementPassword },
            Cancellation);
        Assert.Equal(HttpStatusCode.NoContent, withNewPassword.StatusCode);
    }

    [Fact]
    public async Task A_reset_token_cannot_be_used_twice()
    {
        using var client = factory.CreateAuthClient();
        var email = TestAccounts.NewEmail("reset-replay");
        await client.SignInAsNewAccountAsync(factory, email, TestAccounts.ValidPassword, Cancellation);

        await client.PostAsync("/api/auth/forgot-password", new { email }, Cancellation);
        var notification = factory.Notifications.Latest(AccountNotificationKind.PasswordReset, email);
        Assert.NotNull(notification);

        var (userId, token) = CapturingNotificationSender.ReadLinkParameters(notification);
        await client.PostAsync(
            "/api/auth/reset-password",
            new { userId, token, password = ReplacementPassword },
            Cancellation);

        var replay = await client.PostAsync(
            "/api/auth/reset-password",
            new { userId, token, password = "second-attempt-password-8" },
            Cancellation);

        Assert.Equal(HttpStatusCode.BadRequest, replay.StatusCode);
        Assert.Equal("invalid_token", await TestAccounts.ReadProblemCodeAsync(replay, Cancellation));
    }

    [Fact]
    public async Task Forgot_password_answers_the_same_for_a_stranger_as_for_an_account()
    {
        using var client = factory.CreateAuthClient();
        var registered = TestAccounts.NewEmail("reset-known");
        await client.SignInAsNewAccountAsync(factory, registered, TestAccounts.ValidPassword, Cancellation);

        var known = await client.PostAsync("/api/auth/forgot-password", new { email = registered }, Cancellation);
        var knownBody = await TestAccounts.ReadComparableBodyAsync(known, Cancellation);

        var unknown = await client.PostAsync(
            "/api/auth/forgot-password",
            new { email = TestAccounts.NewEmail("reset-unknown") },
            Cancellation);
        var unknownBody = await TestAccounts.ReadComparableBodyAsync(unknown, Cancellation);

        Assert.Equal(HttpStatusCode.Accepted, known.StatusCode);
        Assert.Equal(known.StatusCode, unknown.StatusCode);
        Assert.Equal(knownBody, unknownBody);
    }

    [Fact]
    public async Task No_notification_is_produced_for_an_address_with_no_account()
    {
        using var client = factory.CreateAuthClient();
        var stranger = TestAccounts.NewEmail("reset-nobody");

        await client.PostAsync("/api/auth/forgot-password", new { email = stranger }, Cancellation);

        Assert.Null(factory.Notifications.Latest(AccountNotificationKind.PasswordReset, stranger));
    }

    [Fact]
    public async Task A_tampered_token_is_refused()
    {
        using var client = factory.CreateAuthClient();
        var email = TestAccounts.NewEmail("reset-tampered");
        await client.SignInAsNewAccountAsync(factory, email, TestAccounts.ValidPassword, Cancellation);

        await client.PostAsync("/api/auth/forgot-password", new { email }, Cancellation);
        var notification = factory.Notifications.Latest(AccountNotificationKind.PasswordReset, email);
        Assert.NotNull(notification);

        var (userId, token) = CapturingNotificationSender.ReadLinkParameters(notification);

        var response = await client.PostAsync(
            "/api/auth/reset-password",
            new { userId, token = token[..^4] + "AAAA", password = ReplacementPassword },
            Cancellation);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("invalid_token", await TestAccounts.ReadProblemCodeAsync(response, Cancellation));
    }

    [Fact]
    public async Task A_reset_password_below_the_policy_is_rejected()
    {
        using var client = factory.CreateAuthClient();
        var email = TestAccounts.NewEmail("reset-weak");
        await client.SignInAsNewAccountAsync(factory, email, TestAccounts.ValidPassword, Cancellation);

        await client.PostAsync("/api/auth/forgot-password", new { email }, Cancellation);
        var notification = factory.Notifications.Latest(AccountNotificationKind.PasswordReset, email);
        Assert.NotNull(notification);

        var (userId, token) = CapturingNotificationSender.ReadLinkParameters(notification);
        var response = await client.PostAsync(
            "/api/auth/reset-password",
            new { userId, token, password = "short" },
            Cancellation);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var body = await TestAccounts.ReadBodyAsync(response, Cancellation);
        Assert.True(body.GetProperty("errors").TryGetProperty("password", out _));
    }
}
