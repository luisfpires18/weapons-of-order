namespace WeaponsOfOrder.Api.Auth.Notifications;

public enum AccountNotificationKind
{
    EmailConfirmation,
    PasswordReset,
}

/// <summary>
/// One account message the server needs delivered to a mailbox.
/// </summary>
/// <remarks>
/// <paramref name="Link"/> carries a single-use Identity token and is therefore a bearer
/// credential: it may be handed to a delivery provider, but it must never be written to
/// ordinary logs or persisted outside development.
/// </remarks>
public sealed record AccountNotification(
    AccountNotificationKind Kind,
    string Email,
    string Link,
    DateTimeOffset CreatedAt);

/// <summary>
/// Delivery seam for account messages. Browser V1 has no production email provider yet;
/// selecting and configuring one is deployment work, not a change to these flows.
/// </summary>
public interface IAccountNotificationSender
{
    Task SendAsync(AccountNotification notification, CancellationToken cancellationToken);
}
