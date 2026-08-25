using System.Net;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using WeaponsOfOrder.Api.Auth.Notifications;
using WeaponsOfOrder.Infrastructure.Identity;
using Xunit;

namespace WeaponsOfOrder.Api.Tests;

public sealed class RegistrationTests(WeaponsOfOrderApiFactory factory)
    : IClassFixture<WeaponsOfOrderApiFactory>
{
    private CancellationToken Cancellation => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Valid_registration_persists_an_account_and_sends_a_confirmation()
    {
        using var client = factory.CreateAuthClient();
        var username = TestAccounts.NewUsername("register-valid");
        var email = TestAccounts.NewEmail("register-valid");

        var response = await client.PostAsync(
            "/api/auth/register",
            new { username, email, password = TestAccounts.ValidPassword },
            Cancellation);

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);

        using var scope = factory.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<WeaponsOfOrderUser>>();
        var stored = await users.FindByEmailAsync(email);

        Assert.NotNull(stored);
        Assert.False(stored.EmailConfirmed);
        // The password is stored as an Identity hash, never as anything reversible.
        Assert.NotNull(stored.PasswordHash);
        Assert.DoesNotContain(TestAccounts.ValidPassword, stored.PasswordHash, StringComparison.Ordinal);

        Assert.NotNull(factory.Notifications.Latest(AccountNotificationKind.EmailConfirmation, email));
    }

    /// <summary>
    /// The name is what Identity's <c>UserName</c> now holds, rather than a copy of the
    /// address the old registration wrote there. The Guid stays the account's identity.
    /// </summary>
    [Fact]
    public async Task The_username_is_stored_as_its_own_value_beside_the_address()
    {
        using var client = factory.CreateAuthClient();
        var username = TestAccounts.NewUsername("register-username");
        var email = TestAccounts.NewEmail("register-username");

        await client.RegisterAsync(username, email, TestAccounts.ValidPassword, Cancellation);

        using var scope = factory.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<WeaponsOfOrderUser>>();
        var stored = await users.FindByEmailAsync(email);

        Assert.NotNull(stored);
        Assert.Equal(username, stored.UserName);
        Assert.Equal(email, stored.Email);
        Assert.NotEqual(stored.Email, stored.UserName);
        Assert.NotEqual(Guid.Empty, stored.Id);

        // The same row is reachable by either lookup, which is what the login field relies on.
        var byName = await users.FindByNameAsync(username);
        Assert.NotNull(byName);
        Assert.Equal(stored.Id, byName.Id);
    }

    [Fact]
    public async Task Registration_normalizes_the_submitted_address()
    {
        using var client = factory.CreateAuthClient();
        var email = TestAccounts.NewEmail("register-case");

        var response = await client.PostAsync(
            "/api/auth/register",
            new
            {
                username = $"  {TestAccounts.NewUsername("register-case")}  ",
                email = $"  {email.ToUpperInvariant()}  ",
                password = TestAccounts.ValidPassword,
            },
            Cancellation);

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);

        using var scope = factory.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<WeaponsOfOrderUser>>();
        var stored = await users.FindByEmailAsync(email);

        Assert.NotNull(stored);
        // Surrounding whitespace is trimmed off the name rather than stored as part of it.
        Assert.NotNull(stored.UserName);
        Assert.Equal(stored.UserName.Trim(), stored.UserName);
    }

    [Theory]
    [InlineData("not-an-email")]
    [InlineData("")]
    [InlineData("missing@")]
    public async Task Malformed_addresses_are_rejected_as_field_errors(string email)
    {
        using var client = factory.CreateAuthClient();

        var response = await client.PostAsync(
            "/api/auth/register",
            new
            {
                username = TestAccounts.NewUsername("register-bad-email"),
                email,
                password = TestAccounts.ValidPassword,
            },
            Cancellation);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("validation", await TestAccounts.ReadProblemCodeAsync(response, Cancellation));

        var errors = (await TestAccounts.ReadBodyAsync(response, Cancellation)).GetProperty("errors");

        Assert.True(errors.TryGetProperty("email", out _));
        Assert.False(errors.TryGetProperty("username", out _));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task A_blank_username_is_rejected_at_the_username_field(string username)
    {
        using var client = factory.CreateAuthClient();

        var response = await client.PostAsync(
            "/api/auth/register",
            new
            {
                username,
                email = TestAccounts.NewEmail("register-blank-username"),
                password = TestAccounts.ValidPassword,
            },
            Cancellation);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var errors = (await TestAccounts.ReadBodyAsync(response, Cancellation)).GetProperty("errors");

        Assert.True(errors.TryGetProperty("username", out _));
        Assert.False(errors.TryGetProperty("email", out _));
    }

    /// <summary>
    /// The one structural restriction: an identifier carrying an at sign has to be an address
    /// or the single login field could name two different accounts.
    /// </summary>
    [Theory]
    [InlineData("smith@weaponsoforder.test")]
    [InlineData("smith@")]
    [InlineData("@smith")]
    public async Task A_username_containing_an_at_sign_is_rejected(string username)
    {
        using var client = factory.CreateAuthClient();

        var response = await client.PostAsync(
            "/api/auth/register",
            new
            {
                username,
                email = TestAccounts.NewEmail("register-at-username"),
                password = TestAccounts.ValidPassword,
            },
            Cancellation);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var errors = (await TestAccounts.ReadBodyAsync(response, Cancellation)).GetProperty("errors");
        var messages = errors.GetProperty("username").EnumerateArray().Select(value => value.GetString());

        Assert.Contains(messages, message => message?.Contains('@', StringComparison.Ordinal) == true);
    }

    /// <summary>
    /// Identity's default AllowedUserNameCharacters whitelist would refuse these. The Browser
    /// V1 rule is non-empty, no at sign, unique — and it is the authority.
    /// </summary>
    [Theory]
    [InlineData("Únreally")]
    [InlineData("un really")]
    [InlineData("un!really")]
    public async Task A_name_outside_Identitys_default_whitelist_is_still_accepted(string prefix)
    {
        using var client = factory.CreateAuthClient();
        var username = $"{prefix}-{Guid.CreateVersion7():n}";

        var response = await client.PostAsync(
            "/api/auth/register",
            new
            {
                username,
                email = TestAccounts.NewEmail("register-charset"),
                password = TestAccounts.ValidPassword,
            },
            Cancellation);

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);

        using var scope = factory.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<WeaponsOfOrderUser>>();

        Assert.NotNull(await users.FindByNameAsync(username));
    }

    [Fact]
    public async Task A_taken_username_is_reported_at_the_username_field()
    {
        using var client = factory.CreateAuthClient();
        var username = TestAccounts.NewUsername("register-name-taken");

        await client.RegisterAsync(
            username,
            TestAccounts.NewEmail("register-name-taken-first"),
            TestAccounts.ValidPassword,
            Cancellation);

        var response = await client.PostAsync(
            "/api/auth/register",
            new
            {
                username,
                email = TestAccounts.NewEmail("register-name-taken-second"),
                password = TestAccounts.ValidPassword,
            },
            Cancellation);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("validation", await TestAccounts.ReadProblemCodeAsync(response, Cancellation));

        var errors = (await TestAccounts.ReadBodyAsync(response, Cancellation)).GetProperty("errors");

        // Said plainly: a username is chosen to be seen, and hiding the collision would leave
        // the player with no way to pick a name that works.
        Assert.True(errors.TryGetProperty("username", out _));
        Assert.False(errors.TryGetProperty("email", out _));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Username_uniqueness_ignores_case(bool upper)
    {
        using var client = factory.CreateAuthClient();
        var username = TestAccounts.NewUsername("Register-Name-Case");

        await client.RegisterAsync(
            username,
            TestAccounts.NewEmail("register-name-case-first"),
            TestAccounts.ValidPassword,
            Cancellation);

        var response = await client.PostAsync(
            "/api/auth/register",
            new
            {
                username = upper ? username.ToUpperInvariant() : username.ToLowerInvariant(),
                email = TestAccounts.NewEmail("register-name-case-second"),
                password = TestAccounts.ValidPassword,
            },
            Cancellation);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.True((await TestAccounts.ReadBodyAsync(response, Cancellation))
            .GetProperty("errors")
            .TryGetProperty("username", out _));
    }

    [Fact]
    public async Task A_bad_address_and_a_bad_password_are_reported_together()
    {
        using var client = factory.CreateAuthClient();

        var response = await client.PostAsync(
            "/api/auth/register",
            new { username = "", email = "not-an-email", password = TestAccounts.TooShortPassword },
            Cancellation);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var errors = (await TestAccounts.ReadBodyAsync(response, Cancellation)).GetProperty("errors");

        Assert.True(errors.TryGetProperty("username", out _));
        Assert.True(errors.TryGetProperty("email", out _));
        Assert.True(errors.TryGetProperty("password", out _));
    }

    [Fact]
    public async Task A_password_below_the_policy_is_rejected_without_creating_an_account()
    {
        using var client = factory.CreateAuthClient();
        var email = TestAccounts.NewEmail("register-weak");

        var response = await client.PostAsync(
            "/api/auth/register",
            new
            {
                username = TestAccounts.NewUsername("register-weak"),
                email,
                password = TestAccounts.TooShortPassword,
            },
            Cancellation);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var body = await TestAccounts.ReadBodyAsync(response, Cancellation);
        Assert.True(body.GetProperty("errors").TryGetProperty("password", out _));

        using var scope = factory.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<WeaponsOfOrderUser>>();

        Assert.Null(await users.FindByEmailAsync(email));
    }

    /// <summary>
    /// Length is the whole rule. Six characters is enough however unvaried they are, and five
    /// is not enough however varied.
    /// </summary>
    /// <remarks>
    /// One case is not ours to choose: Identity's own <c>PasswordValidator</c> reports an
    /// entirely whitespace password as too short whatever its length. That is framework
    /// behaviour rather than a composition rule this policy adds, and displacing it would mean
    /// replacing the validator.
    /// </remarks>
    [Theory]
    [InlineData(TestAccounts.ShortestValidPassword)]
    [InlineData("aaaaaa")]
    [InlineData("!!!!!!")]
    [InlineData("......")]
    [InlineData("passwd")]
    public async Task A_six_character_password_is_accepted_whatever_it_is_made_of(string password)
    {
        using var client = factory.CreateAuthClient();
        var email = TestAccounts.NewEmail("register-policy-ok");

        var response = await client.PostAsync(
            "/api/auth/register",
            new { username = TestAccounts.NewUsername("register-policy-ok"), email, password },
            Cancellation);

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);

        using var scope = factory.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<WeaponsOfOrderUser>>();

        Assert.NotNull(await users.FindByEmailAsync(email));
    }

    [Theory]
    [InlineData(TestAccounts.TooShortPassword)]
    [InlineData("aaaaa")]
    [InlineData("Aa1!x")]
    public async Task A_five_character_password_is_refused_however_varied(string password)
    {
        using var client = factory.CreateAuthClient();

        var response = await client.PostAsync(
            "/api/auth/register",
            new
            {
                username = TestAccounts.NewUsername("register-policy-short"),
                email = TestAccounts.NewEmail("register-policy-short"),
                password,
            },
            Cancellation);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.True((await TestAccounts.ReadBodyAsync(response, Cancellation))
            .GetProperty("errors")
            .TryGetProperty("password", out _));
    }

    [Fact]
    public async Task Registering_a_taken_address_answers_exactly_as_a_new_one_does()
    {
        using var client = factory.CreateAuthClient();
        var email = TestAccounts.NewEmail("register-duplicate");

        var first = await client.PostAsync(
            "/api/auth/register",
            new
            {
                username = TestAccounts.NewUsername("register-duplicate-first"),
                email,
                password = TestAccounts.ValidPassword,
            },
            Cancellation);
        var firstBody = await TestAccounts.ReadComparableBodyAsync(first, Cancellation);

        // A free username, so the only thing that could be reported is the address.
        var second = await client.PostAsync(
            "/api/auth/register",
            new
            {
                username = TestAccounts.NewUsername("register-duplicate-second"),
                email,
                password = "a-completely-different-password-9",
            },
            Cancellation);
        var secondBody = await TestAccounts.ReadComparableBodyAsync(second, Cancellation);

        // Status and body are identical, so the response cannot be used to test which
        // addresses are registered.
        Assert.Equal(first.StatusCode, second.StatusCode);
        Assert.Equal(firstBody, secondBody);

        using var scope = factory.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<WeaponsOfOrderUser>>();
        var stored = await users.FindByEmailAsync(email);

        Assert.NotNull(stored);
        // The second attempt must not have overwritten the first account's password, and must
        // not have renamed it either.
        Assert.Equal(
            PasswordVerificationResult.Success,
            users.PasswordHasher.VerifyHashedPassword(stored, stored.PasswordHash!, TestAccounts.ValidPassword));
        Assert.StartsWith("register-duplicate-first", stored.UserName!, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_weak_password_for_a_taken_address_still_reports_only_the_password()
    {
        using var client = factory.CreateAuthClient();
        var email = TestAccounts.NewEmail("register-duplicate-weak");

        await client.RegisterAsync(
            TestAccounts.NewUsername("register-duplicate-weak-first"),
            email,
            TestAccounts.ValidPassword,
            Cancellation);

        var response = await client.PostAsync(
            "/api/auth/register",
            new
            {
                username = TestAccounts.NewUsername("register-duplicate-weak-second"),
                email,
                password = TestAccounts.TooShortPassword,
            },
            Cancellation);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var body = await TestAccounts.ReadBodyAsync(response, Cancellation);
        var errors = body.GetProperty("errors");

        Assert.True(errors.TryGetProperty("password", out _));
        Assert.False(errors.TryGetProperty("email", out _));
        Assert.False(errors.TryGetProperty("username", out _));
    }

    /// <summary>
    /// A taken name with a taken address is answered as a taken name. That is not a
    /// disclosure: the name collision is true on its own and says nothing about the address.
    /// </summary>
    [Fact]
    public async Task A_taken_username_wins_over_a_taken_address()
    {
        using var client = factory.CreateAuthClient();
        var username = TestAccounts.NewUsername("register-both-taken");
        var email = TestAccounts.NewEmail("register-both-taken");

        await client.RegisterAsync(username, email, TestAccounts.ValidPassword, Cancellation);

        var response = await client.PostAsync(
            "/api/auth/register",
            new { username, email, password = TestAccounts.ValidPassword },
            Cancellation);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.True((await TestAccounts.ReadBodyAsync(response, Cancellation))
            .GetProperty("errors")
            .TryGetProperty("username", out _));
    }
}
