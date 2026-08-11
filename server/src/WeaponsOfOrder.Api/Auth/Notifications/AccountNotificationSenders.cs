using Microsoft.Extensions.Logging;

namespace WeaponsOfOrder.Api.Auth.Notifications;

/// <summary>
/// DEVELOPMENT ONLY delivery. Captures the link in <see cref="DevelopmentNotificationOutbox"/>
/// and writes it to the server log so a local developer can finish the flow.
/// </summary>
/// <remarks>
/// This deliberately violates the production rule that tokens never reach logs, which is
/// exactly why it is registered only in the Development environment. Every other
/// environment gets <see cref="UnconfiguredAccountNotificationSender"/>, which never
/// records the link anywhere.
/// </remarks>
internal sealed class DevelopmentAccountNotificationSender(
    DevelopmentNotificationOutbox outbox,
    ILogger<DevelopmentAccountNotificationSender> logger) : IAccountNotificationSender
{
    public Task SendAsync(AccountNotification notification, CancellationToken cancellationToken)
    {
        outbox.Add(notification);

        logger.LogWarning(
            "DEVELOPMENT ONLY - no email was sent. {Kind} link for {Email}: {Link}",
            notification.Kind,
            notification.Email,
            notification.Link);

        return Task.CompletedTask;
    }
}

/// <summary>
/// The sender used wherever no production email provider has been configured yet.
/// </summary>
/// <remarks>
/// Records that a message was dropped, identified only by account id, and never the link
/// itself. Failing loudly here instead would let anyone turn "forgot password" into a
/// 500, so the request still completes with its normal non-enumerating response.
/// </remarks>
internal sealed class UnconfiguredAccountNotificationSender(
    ILogger<UnconfiguredAccountNotificationSender> logger) : IAccountNotificationSender
{
    public Task SendAsync(AccountNotification notification, CancellationToken cancellationToken)
    {
        logger.LogError(
            "No email delivery provider is configured; a {Kind} notification was dropped.",
            notification.Kind);

        return Task.CompletedTask;
    }
}
