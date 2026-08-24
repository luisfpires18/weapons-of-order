using Microsoft.Extensions.Options;

namespace WeaponsOfOrder.Api.Auth.Notifications;

/// <summary>
/// Refuses to start when a delivery provider is selected but cannot be reached.
/// </summary>
/// <remarks>
/// Checked at startup for the same reason as <see cref="AuthOptionsValidator"/>: a
/// half-configured sender is not discovered until somebody registers, and at that point the
/// only symptom is an account nobody can confirm. Refusing to boot is the failure a
/// deployment can see.
/// </remarks>
internal sealed class EmailOptionsValidator : IValidateOptions<EmailOptions>
{
    public ValidateOptionsResult Validate(string? name, EmailOptions options)
    {
        if (options.Provider == EmailProvider.None)
        {
            return ValidateOptionsResult.Success;
        }

        var failures = new List<string>();

        if (string.IsNullOrWhiteSpace(options.SenderAddress))
        {
            failures.Add(
                $"'{EmailOptions.SectionName}:{nameof(EmailOptions.SenderAddress)}' is required. It must be an "
                + "address on a domain the provider has verified.");
        }

        if (options.Provider == EmailProvider.AzureCommunicationServices)
        {
            var settings = options.AzureCommunicationServices;
            var hasEndpoint = Uri.TryCreate(settings.Endpoint, UriKind.Absolute, out var endpoint)
                && endpoint.Scheme == Uri.UriSchemeHttps;

            if (!hasEndpoint && string.IsNullOrWhiteSpace(settings.ConnectionString))
            {
                failures.Add(
                    $"'{EmailOptions.SectionName}:{nameof(EmailOptions.AzureCommunicationServices)}' needs either an "
                    + "absolute https 'Endpoint', which authenticates as the hosting managed identity, or a "
                    + "'ConnectionString'. Prefer the endpoint: it needs no secret.");
            }
        }

        return failures.Count == 0
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(failures);
    }
}
