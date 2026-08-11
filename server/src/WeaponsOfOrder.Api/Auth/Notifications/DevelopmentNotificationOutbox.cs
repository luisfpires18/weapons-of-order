namespace WeaponsOfOrder.Api.Auth.Notifications;

/// <summary>
/// DEVELOPMENT ONLY. Holds the most recent account links in memory so a local developer
/// or a browser test can complete the confirmation and reset flows without an email
/// provider.
/// </summary>
/// <remarks>
/// Registered only in the Development environment. Nothing is written to the database,
/// nothing survives a restart, and the capacity is bounded so a long session cannot grow
/// it without limit.
/// </remarks>
public sealed class DevelopmentNotificationOutbox
{
    private const int Capacity = 25;

    private readonly Lock _gate = new();
    private readonly LinkedList<AccountNotification> _notifications = [];

    public void Add(AccountNotification notification)
    {
        lock (_gate)
        {
            _notifications.AddFirst(notification);
            while (_notifications.Count > Capacity)
            {
                _notifications.RemoveLast();
            }
        }
    }

    /// <summary>Most recent first.</summary>
    public IReadOnlyList<AccountNotification> Snapshot()
    {
        lock (_gate)
        {
            return [.. _notifications];
        }
    }
}
