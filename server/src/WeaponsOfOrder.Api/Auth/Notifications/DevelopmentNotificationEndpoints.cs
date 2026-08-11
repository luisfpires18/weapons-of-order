using Microsoft.Extensions.Options;

namespace WeaponsOfOrder.Api.Auth.Notifications;

/// <summary>
/// DEVELOPMENT ONLY. Publishes the captured confirmation and reset links so a local
/// developer or a browser test can complete those flows without an email provider.
/// </summary>
/// <remarks>
/// Mapping is guarded twice: the caller only invokes this in the Development environment,
/// and <c>Auth:Development:ExposeNotifications</c> can switch it off there as well. It is
/// never reachable in any other environment, where the sender does not record links at all.
/// <para>
/// The public account endpoints behave identically with or without this endpoint. It reads
/// an in-memory capture; it does not change what "forgot password" tells a stranger.
/// </para>
/// </remarks>
internal static class DevelopmentNotificationEndpoints
{
    public static IEndpointRouteBuilder MapDevelopmentAccountNotifications(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/dev/account-notifications", (
            DevelopmentNotificationOutbox outbox,
            IOptions<AuthOptions> options) =>
        {
            if (!options.Value.Development.ExposeNotifications)
            {
                return Results.NotFound();
            }

            // Projected rather than returned directly so the kind reads as a name instead of
            // the enum's ordinal, which would silently change meaning if the enum is reordered.
            return Results.Ok(outbox.Snapshot()
                .Select(notification => new CapturedNotification(
                    notification.Kind.ToString(),
                    notification.Email,
                    notification.Link,
                    notification.CreatedAt)));
        });

        return endpoints;
    }

    private sealed record CapturedNotification(string Kind, string Email, string Link, DateTimeOffset CreatedAt);
}
