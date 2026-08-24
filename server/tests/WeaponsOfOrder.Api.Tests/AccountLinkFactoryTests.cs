using Microsoft.Extensions.Options;
using WeaponsOfOrder.Api.Auth;
using WeaponsOfOrder.Api.Auth.Notifications;
using Xunit;

namespace WeaponsOfOrder.Api.Tests;

/// <summary>
/// The origin of an account link comes only from configuration. There is no fallback to the
/// request, because the Host header is attacker-controlled: a reset link built from it could
/// be addressed to the attacker's domain and mailed to somebody else.
/// </summary>
public sealed class AccountLinkFactoryTests
{
    private static readonly Guid AccountId = Guid.Parse("019ff0ba-f25b-7383-a239-4d3822f2945d");

    [Theory]
    [InlineData("https://weaponsoforder.example")]
    [InlineData("https://weaponsoforder.example/")]
    [InlineData("https://weaponsoforder.example/play")]
    [InlineData("http://localhost:1337")]
    [InlineData("http://127.0.0.1:1337")]
    public void A_trusted_origin_is_accepted(string configured)
    {
        Assert.True(AccountLinkFactory.TryParseClientBaseUrl(configured, out _));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    // Plain HTTP off a loopback host would put the token on the wire in clear text.
    [InlineData("http://weaponsoforder.example")]
    // Not absolute: nothing here says which host the link belongs to.
    [InlineData("/play")]
    [InlineData("weaponsoforder.example")]
    [InlineData("//weaponsoforder.example")]
    // Foreign schemes have no business being the origin of an emailed link.
    [InlineData("javascript:alert(1)")]
    [InlineData("data:text/html,<script>alert(1)</script>")]
    [InlineData("file:///etc/passwd")]
    [InlineData("ftp://weaponsoforder.example")]
    // A query or fragment would collide with the userId and token the link carries.
    [InlineData("https://weaponsoforder.example?next=/evil")]
    [InlineData("https://weaponsoforder.example#fragment")]
    public void An_untrusted_or_unusable_origin_is_rejected(string? configured)
    {
        Assert.False(AccountLinkFactory.TryParseClientBaseUrl(configured, out _));
    }

    [Fact]
    public void A_configured_factory_builds_a_link_on_that_origin()
    {
        var factory = Create("https://weaponsoforder.example");

        var link = factory.TryBuild(AccountLinkFactory.ResetPasswordPath, AccountId, "token-value");

        Assert.NotNull(link);
        Assert.StartsWith("https://weaponsoforder.example/reset-password?", link, StringComparison.Ordinal);
        Assert.Contains($"userId={AccountId}", link, StringComparison.Ordinal);
        Assert.Contains($"token={AccountLinkFactory.EncodeToken("token-value")}", link, StringComparison.Ordinal);
    }

    [Fact]
    public void A_trailing_slash_does_not_produce_a_doubled_path()
    {
        var factory = Create("https://weaponsoforder.example/");

        var link = factory.TryBuild(AccountLinkFactory.ConfirmEmailPath, AccountId, "token-value");

        Assert.StartsWith("https://weaponsoforder.example/confirm-email?", link, StringComparison.Ordinal);
    }

    [Fact]
    public void An_unconfigured_factory_builds_nothing_rather_than_guessing()
    {
        var factory = Create(clientBaseUrl: null);

        Assert.False(factory.IsConfigured);
        Assert.Null(factory.TryBuild(AccountLinkFactory.ResetPasswordPath, AccountId, "token-value"));
    }

    [Fact]
    public void A_token_survives_the_url_round_trip()
    {
        // Identity tokens are base64 with +, / and = in them, which a raw query string mangles.
        const string awkward = "CfDJ8Abc+def/ghi=jkl";

        Assert.True(AccountLinkFactory.TryDecodeToken(AccountLinkFactory.EncodeToken(awkward), out var decoded));
        Assert.Equal(awkward, decoded);
    }

    private static AccountLinkFactory Create(string? clientBaseUrl)
        => new(Options.Create(new AuthOptions { ClientBaseUrl = clientBaseUrl }));
}

public sealed class AuthOptionsValidatorTests
{
    [Fact]
    public void A_valid_client_origin_passes()
    {
        var result = Validate("https://weaponsoforder.example");

        Assert.True(result.Succeeded);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("http://weaponsoforder.example")]
    [InlineData("javascript:alert(1)")]
    [InlineData("/play")]
    public void An_unusable_client_origin_fails_validation(string? configured)
    {
        var result = Validate(configured);

        Assert.True(result.Failed);
        Assert.Contains("ClientBaseUrl", result.FailureMessage, StringComparison.Ordinal);
    }

    [Fact]
    public void Loopback_http_is_accepted_only_in_development()
    {
        Assert.True(Validate("http://localhost:1337", "Development").Succeeded);
    }

    [Theory]
    [InlineData("Staging")]
    [InlineData("Production")]
    public void A_deployed_environment_refuses_a_loopback_http_origin(string environmentName)
    {
        // Otherwise a deployment inheriting the development default would mail every player
        // a confirmation link pointing at their own machine, over plain http.
        var result = Validate("http://localhost:1337", environmentName);

        Assert.True(result.Failed);
        Assert.Contains("https", result.FailureMessage, StringComparison.Ordinal);
    }

    private static ValidateOptionsResult Validate(
        string? clientBaseUrl,
        string environmentName = "Production")
        => new AuthOptionsValidator(new StubHostEnvironment(environmentName))
            .Validate(name: null, new AuthOptions { ClientBaseUrl = clientBaseUrl });
}

public sealed class MissingClientOriginTests
{
    [Fact]
    public void The_application_refuses_to_start_without_a_trusted_client_origin()
    {
        using var factory = new MissingClientOriginApiFactory();

        // Startup, not the first password reset. A misconfiguration that only surfaces for
        // addresses that exist would be an account-existence oracle.
        var exception = Assert.Throws<OptionsValidationException>(() => _ = factory.Services);

        Assert.Contains("ClientBaseUrl", exception.Message, StringComparison.Ordinal);
    }
}
