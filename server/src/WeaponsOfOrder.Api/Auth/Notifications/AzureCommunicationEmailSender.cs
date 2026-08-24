using System.Net;
using Azure;
using Azure.Communication.Email;
using Microsoft.Extensions.Options;

namespace WeaponsOfOrder.Api.Auth.Notifications;

/// <summary>
/// Delivers account messages through Azure Communication Services Email.
/// </summary>
/// <remarks>
/// <para>
/// The link is a single-use bearer credential. It is handed to the provider and to nothing
/// else: not to a log, not to telemetry, not to an exception message. Neither is the
/// address it was built for, matching what the development sender already does.
/// </para>
/// <para>
/// Never throws. <see cref="IAccountNotificationSender"/> is called from the registration,
/// forgot-password and resend endpoints, which all answer identically whether or not the
/// address belongs to an account. A provider outage that surfaced as a 500 would answer
/// differently for an address that exists than for one that does not, which is the
/// enumeration oracle those endpoints are shaped to avoid. A dropped message is recorded
/// and the caller still gets the ordinary response.
/// </para>
/// </remarks>
internal sealed class AzureCommunicationEmailSender(
    EmailClient client,
    IOptions<EmailOptions> options,
    ILogger<AzureCommunicationEmailSender> logger) : IAccountNotificationSender
{
    public async Task SendAsync(AccountNotification notification, CancellationToken cancellationToken)
    {
        // Both are guaranteed by EmailOptionsValidator, which runs at startup: this sender
        // is only ever composed for a configuration that already passed it.
        var senderAddress = options.Value.SenderAddress!;
        var content = Compose(notification);

        var message = new EmailMessage(
            senderAddress: senderAddress,
            recipients: new EmailRecipients([new EmailAddress(notification.Email)]),
            content: content);

        try
        {
            // Started, not Completed: this runs inside the request, and the caller needs to
            // know the service accepted the message, not to wait for a mailbox to receive
            // it. Delivery status is an Application Insights and provider-side question.
            var operation = await client.SendAsync(WaitUntil.Started, message, cancellationToken);

            // The operation id identifies the message to provider support without
            // identifying the recipient.
            logger.LogInformation(
                "A {Kind} notification was accepted for delivery. Operation {OperationId}.",
                notification.Kind,
                operation.Id);
        }
        catch (RequestFailedException failure)
        {
            // Error code and status only. The provider echoes the submitted address into
            // some messages, and the exception is not worth an account-existence signal.
            logger.LogError(
                "A {Kind} notification was rejected by the email provider: status {Status}, code {Code}.",
                notification.Kind,
                failure.Status,
                failure.ErrorCode ?? "none");
        }
        catch (Exception failure) when (failure is not OperationCanceledException)
        {
            logger.LogError(
                failure,
                "A {Kind} notification could not be handed to the email provider.",
                notification.Kind);
        }
    }

    /// <summary>
    /// Plain text and HTML for the same message. Deliberately plain: this is a credential
    /// delivery, and a mail full of remote images and tracking is exactly what a player is
    /// taught to distrust.
    /// </summary>
    private static EmailContent Compose(AccountNotification notification)
    {
        var (subject, lead, action) = notification.Kind switch
        {
            AccountNotificationKind.EmailConfirmation => (
                "Confirm your Weapons of Order account",
                "Confirm this address to finish setting up your Weapons of Order account.",
                "Confirm my account"),
            AccountNotificationKind.PasswordReset => (
                "Reset your Weapons of Order password",
                "Someone asked to reset the password for this Weapons of Order account. "
                + "If that was not you, ignore this message and nothing changes.",
                "Choose a new password"),
            _ => throw new ArgumentOutOfRangeException(nameof(notification)),
        };

        var link = WebUtility.HtmlEncode(notification.Link);

        return new EmailContent(subject)
        {
            PlainText = $"{lead}\n\n{notification.Link}\n\nThe link can be used once, and expires.",
            Html = $"""
                <html><body style="font-family:system-ui,sans-serif;line-height:1.5">
                <p>{WebUtility.HtmlEncode(lead)}</p>
                <p><a href="{link}">{WebUtility.HtmlEncode(action)}</a></p>
                <p style="color:#555">The link can be used once, and expires.</p>
                </body></html>
                """,
        };
    }
}
