namespace WeaponsOfOrder.Api.Auth.Notifications;

internal enum EmailProvider
{
    /// <summary>
    /// No delivery. Confirmation and reset messages are dropped and only the fact that they
    /// were dropped is recorded. The default, because a fresh clone has no provider.
    /// </summary>
    None,

    /// <summary>Azure Communication Services Email.</summary>
    AzureCommunicationServices,
}

/// <summary>
/// How account messages leave this process, bound from the <c>Email</c> configuration
/// section.
/// </summary>
/// <remarks>
/// AUTH_SECURITY.md treats the delivery provider as a deployment choice rather than part of
/// the account flows, which is why nothing here reaches into how a confirmation or reset is
/// produced. It only decides who carries it.
/// </remarks>
internal sealed class EmailOptions
{
    public const string SectionName = "Email";

    public EmailProvider Provider { get; set; } = EmailProvider.None;

    /// <summary>
    /// The <c>From</c> address. Must belong to a domain the provider has verified; for an
    /// Azure-managed domain that is the generated
    /// <c>DoNotReply@&lt;guid&gt;.azurecomm.net</c> address.
    /// </summary>
    public string? SenderAddress { get; set; }

    public string SenderDisplayName { get; set; } = "Weapons of Order";

    public AzureCommunicationServicesSettings AzureCommunicationServices { get; set; } = new();
}

internal sealed class AzureCommunicationServicesSettings
{
    /// <summary>
    /// The Communication Services resource endpoint. Preferred over
    /// <see cref="ConnectionString"/>: with an endpoint the client authenticates as the
    /// hosting managed identity, so no provider secret has to exist anywhere.
    /// </summary>
    public string? Endpoint { get; set; }

    /// <summary>
    /// Fallback for a host with no managed identity. This value is an access key and is a
    /// secret: it belongs in platform configuration, never in a committed settings file.
    /// </summary>
    public string? ConnectionString { get; set; }

    /// <summary>
    /// Selects a specific user-assigned identity. Left empty, the system-assigned identity
    /// is used, which is what the staging App Service has.
    /// </summary>
    public string? ManagedIdentityClientId { get; set; }
}
