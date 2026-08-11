using Microsoft.Extensions.Logging;

namespace WeaponsOfOrder.Api.Auth.Notifications;

/// <summary>
/// DEVELOPMENT ONLY delivery. Captures the link in <see cref="DevelopmentNotificationOutbox"/>,
/// which the development-only notifications endpoint reads, so a local developer can finish
/// the confirmation and reset flows without an email provider.
/// </summary>
/// <remarks>
/// The link is a bearer credential and is never written to a log, not even here. Logs get
/// copied, shipped and retained in places the outbox is not: the outbox lives in memory,
/// is bounded, and disappears with the process.
/// </remarks>
internal sealed class DevelopmentAccountNotificationSender(
    DevelopmentNotificationOutbox outbox,
    ILogger<DevelopmentAccountNotificationSender> logger) : IAccountNotificationSender
{
    public Task SendAsync(AccountNotification notification, CancellationToken cancellationToken)
    {
        outbox.Add(notification);

        // Kind only. Neither the link nor the address it was built for goes to the log.
        logger.LogInformation(
            "DEVELOPMENT ONLY - no email was sent. A {Kind} link was captured; read it from "
            + "GET /api/dev/account-notifications.",
            notification.Kind);

        return Task.CompletedTask;
    }
}

/// <summary>
/// The sender used wherever no production email provider has been configured yet.
/// </summary>
/// <remarks>
/// Records that a message was dropped and nothing else. Failing loudly here instead would
/// let anyone turn "forgot password" into a 500, so the request still completes with its
/// normal non-enumerating response.
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
