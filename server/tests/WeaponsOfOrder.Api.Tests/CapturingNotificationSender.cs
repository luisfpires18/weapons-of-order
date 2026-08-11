using Microsoft.AspNetCore.WebUtilities;
using WeaponsOfOrder.Api.Auth.Notifications;

namespace WeaponsOfOrder.Api.Tests;

/// <summary>
/// Stands in for a delivery provider so tests can read the link a mailbox would have
/// received, without the account endpoints behaving any differently.
/// </summary>
public sealed class CapturingNotificationSender : IAccountNotificationSender
{
    private readonly Lock _gate = new();
    private readonly List<AccountNotification> _sent = [];

    public Task SendAsync(AccountNotification notification, CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            _sent.Add(notification);
        }

        return Task.CompletedTask;
    }

    public IReadOnlyList<AccountNotification> Sent
    {
        get
        {
            lock (_gate)
            {
                return [.. _sent];
            }
        }
    }

    public AccountNotification? Latest(AccountNotificationKind kind, string email) => Sent
        .LastOrDefault(notification => notification.Kind == kind
            && string.Equals(notification.Email, email, StringComparison.OrdinalIgnoreCase));

    /// <summary>Pulls the account id and token back out of a captured link.</summary>
    public static (string UserId, string Token) ReadLinkParameters(AccountNotification notification)
    {
        var query = QueryHelpers.ParseQuery(new Uri(notification.Link).Query);
        return (query["userId"].ToString(), query["token"].ToString());
    }
}
