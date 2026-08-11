using WeaponsOfOrder.Api.Auth.Notifications;
using Xunit;

namespace WeaponsOfOrder.Api.Tests;

public sealed class DevelopmentNotificationOutboxTests
{
    [Fact]
    public void The_newest_notification_is_first()
    {
        var outbox = new DevelopmentNotificationOutbox();

        outbox.Add(Notification("first@weaponsoforder.test"));
        outbox.Add(Notification("second@weaponsoforder.test"));

        Assert.Equal("second@weaponsoforder.test", outbox.Snapshot()[0].Email);
    }

    [Fact]
    public void Capacity_is_bounded_so_a_long_session_cannot_grow_it_without_limit()
    {
        var outbox = new DevelopmentNotificationOutbox();

        for (var index = 0; index < 200; index++)
        {
            outbox.Add(Notification($"account-{index}@weaponsoforder.test"));
        }

        var snapshot = outbox.Snapshot();

        Assert.InRange(snapshot.Count, 1, 25);
        Assert.Equal("account-199@weaponsoforder.test", snapshot[0].Email);
    }

    private static AccountNotification Notification(string email) => new(
        AccountNotificationKind.PasswordReset,
        email,
        "https://localhost/reset-password?userId=x&token=y",
        DateTimeOffset.UtcNow);
}
