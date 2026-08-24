using System.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using WeaponsOfOrder.Api.Auth.Notifications;
using WeaponsOfOrder.Api.Telemetry;
using Xunit;

namespace WeaponsOfOrder.Api.Tests;

/// <summary>
/// A half-configured sender is not discovered until somebody registers, and the only
/// symptom then is an account nobody can confirm. These are the checks that turn that into
/// a startup failure.
/// </summary>
public sealed class EmailOptionsValidatorTests
{
    [Fact]
    public void No_provider_is_a_valid_configuration()
    {
        // What a fresh clone has. Messages are dropped and recorded as dropped; the
        // application still starts, because local development reads its links from the
        // in-memory outbox instead.
        var result = Validate(new EmailOptions());

        Assert.True(result.Succeeded);
    }

    [Fact]
    public void A_managed_identity_endpoint_is_enough()
    {
        var result = Validate(new EmailOptions
        {
            Provider = EmailProvider.AzureCommunicationServices,
            SenderAddress = "DoNotReply@example.azurecomm.net",
            AzureCommunicationServices = new AzureCommunicationServicesSettings
            {
                Endpoint = "https://weapons-of-order.europe.communication.azure.com",
            },
        });

        Assert.True(result.Succeeded);
    }

    [Fact]
    public void A_provider_with_no_sender_address_fails()
    {
        var result = Validate(new EmailOptions
        {
            Provider = EmailProvider.AzureCommunicationServices,
            AzureCommunicationServices = new AzureCommunicationServicesSettings
            {
                Endpoint = "https://weapons-of-order.europe.communication.azure.com",
            },
        });

        Assert.True(result.Failed);
        Assert.Contains("SenderAddress", result.FailureMessage, StringComparison.Ordinal);
    }

    [Fact]
    public void A_provider_with_neither_endpoint_nor_connection_string_fails()
    {
        var result = Validate(new EmailOptions
        {
            Provider = EmailProvider.AzureCommunicationServices,
            SenderAddress = "DoNotReply@example.azurecomm.net",
        });

        Assert.True(result.Failed);
        Assert.Contains("Endpoint", result.FailureMessage, StringComparison.Ordinal);
    }

    [Fact]
    public void A_plain_http_endpoint_is_not_accepted_as_an_endpoint()
    {
        // Falls through to needing a connection string rather than quietly talking to a
        // provider over the clear.
        var result = Validate(new EmailOptions
        {
            Provider = EmailProvider.AzureCommunicationServices,
            SenderAddress = "DoNotReply@example.azurecomm.net",
            AzureCommunicationServices = new AzureCommunicationServicesSettings
            {
                Endpoint = "http://weapons-of-order.europe.communication.azure.com",
            },
        });

        Assert.True(result.Failed);
    }

    private static Microsoft.Extensions.Options.ValidateOptionsResult Validate(EmailOptions options)
        => new EmailOptionsValidator().Validate(name: null, options);
}

/// <summary>
/// Which sender a deployed environment actually gets, and whether the client it needs can be
/// built at all. Composed lazily from options, so this is the same code path a staging host
/// takes on its first notification.
/// </summary>
public sealed class AccountNotificationDeliveryTests
{
    [Fact]
    public void No_provider_yields_the_sender_that_only_records_the_drop()
    {
        var sender = AccountNotificationDelivery.Create(Services(), new EmailOptions());

        Assert.IsType<UnconfiguredAccountNotificationSender>(sender);
    }

    [Fact]
    public void A_managed_identity_endpoint_yields_a_usable_azure_sender()
    {
        // Proves the endpoint and credential path constructs. A staging host that could not
        // build this client would fail on somebody's first registration instead.
        var sender = AccountNotificationDelivery.Create(Services(), new EmailOptions
        {
            Provider = EmailProvider.AzureCommunicationServices,
            SenderAddress = "DoNotReply@example.azurecomm.net",
            AzureCommunicationServices = new AzureCommunicationServicesSettings
            {
                Endpoint = "https://example.europe.communication.azure.com",
            },
        });

        Assert.IsType<AzureCommunicationEmailSender>(sender);
    }

    [Fact]
    public void The_provider_binds_from_configuration_by_name()
    {
        // The application setting is the string "AzureCommunicationServices". If the binder
        // ever stopped resolving it, staging would silently fall back to dropping mail.
        var options = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Email:Provider"] = "AzureCommunicationServices",
                ["Email:SenderAddress"] = "DoNotReply@example.azurecomm.net",
                ["Email:AzureCommunicationServices:Endpoint"] = "https://example.europe.communication.azure.com",
            })
            .Build()
            .GetSection(EmailOptions.SectionName)
            .Get<EmailOptions>();

        Assert.NotNull(options);
        Assert.Equal(EmailProvider.AzureCommunicationServices, options.Provider);
        Assert.Equal("https://example.europe.communication.azure.com", options.AzureCommunicationServices.Endpoint);
    }

    private static IServiceProvider Services()
        => new ServiceCollection().AddLogging().BuildServiceProvider();
}

/// <summary>
/// A confirmation link is <c>/confirm-email?userId=…&amp;token=…</c>, and following it is an
/// ordinary navigation this server answers. Without redaction every confirmation and every
/// password reset would write its single-use token into Application Insights.
/// </summary>
public sealed class TelemetryRedactionTests
{
    [Fact]
    public void The_query_string_never_reaches_the_exporter()
    {
        using var activity = new Activity("test");
        activity.SetTag("url.path", "/confirm-email");
        activity.SetTag("url.query", "?userId=abc&token=SECRET-TOKEN");
        activity.SetTag("url.full", "https://staging.example/confirm-email?userId=abc&token=SECRET-TOKEN");

        new QueryStringRedactingProcessor().OnEnd(activity);

        Assert.Null(activity.GetTagItem("url.query"));
        Assert.Equal("https://staging.example/confirm-email", activity.GetTagItem("url.full"));
        Assert.Equal("/confirm-email", activity.GetTagItem("url.path"));
    }

    [Fact]
    public void A_fragment_is_cut_as_well()
    {
        using var activity = new Activity("test");
        activity.SetTag("http.url", "https://staging.example/reset-password#token=SECRET-TOKEN");

        new QueryStringRedactingProcessor().OnEnd(activity);

        Assert.Equal("https://staging.example/reset-password", activity.GetTagItem("http.url"));
    }

    [Fact]
    public void A_url_with_nothing_to_strip_is_left_alone()
    {
        using var activity = new Activity("test");
        activity.SetTag("url.full", "https://staging.example/api/health");

        new QueryStringRedactingProcessor().OnEnd(activity);

        Assert.Equal("https://staging.example/api/health", activity.GetTagItem("url.full"));
    }
}
