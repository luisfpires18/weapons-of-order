using Azure.Communication.Email;
using Azure.Identity;
using Microsoft.Extensions.Options;

namespace WeaponsOfOrder.Api.Auth.Notifications;

/// <summary>
/// Chooses which <see cref="IAccountNotificationSender"/> a deployed environment gets, from
/// its <see cref="EmailOptions"/>.
/// </summary>
/// <remarks>
/// A function rather than a branch inside the composition extension, so the choice — and the
/// client construction it implies — can be exercised without standing up a host.
/// </remarks>
internal static class AccountNotificationDelivery
{
    public static IAccountNotificationSender Create(IServiceProvider services, EmailOptions options)
        => options.Provider switch
        {
            EmailProvider.AzureCommunicationServices => new AzureCommunicationEmailSender(
                CreateEmailClient(options.AzureCommunicationServices),
                Options.Create(options),
                services.GetRequiredService<ILogger<AzureCommunicationEmailSender>>()),

            // Records that a message was dropped and nothing else. Failing loudly here would
            // let anyone turn "forgot password" into a 500, so the request still completes
            // with its normal non-enumerating response.
            _ => new UnconfiguredAccountNotificationSender(
                services.GetRequiredService<ILogger<UnconfiguredAccountNotificationSender>>()),
        };

    /// <summary>
    /// Prefers the managed identity: an endpoint plus a credential means no provider secret
    /// exists to leak, rotate or accidentally commit. The connection string carries an
    /// access key and is the fallback for a host without an identity.
    /// </summary>
    /// <remarks>
    /// Both forms are guaranteed present by <see cref="EmailOptionsValidator"/>, which runs
    /// at startup: this is only reached for a configuration that already passed it.
    /// </remarks>
    private static EmailClient CreateEmailClient(AzureCommunicationServicesSettings settings)
    {
        if (Uri.TryCreate(settings.Endpoint, UriKind.Absolute, out var endpoint)
            && endpoint.Scheme == Uri.UriSchemeHttps)
        {
            return new EmailClient(
                endpoint,
                new DefaultAzureCredential(new DefaultAzureCredentialOptions
                {
                    ManagedIdentityClientId = string.IsNullOrWhiteSpace(settings.ManagedIdentityClientId)
                        ? null
                        : settings.ManagedIdentityClientId,
                }));
        }

        return new EmailClient(settings.ConnectionString!);
    }
}
