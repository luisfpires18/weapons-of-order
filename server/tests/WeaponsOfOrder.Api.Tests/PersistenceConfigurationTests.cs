using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using WeaponsOfOrder.Infrastructure;
using WeaponsOfOrder.Infrastructure.Persistence;
using Xunit;

namespace WeaponsOfOrder.Api.Tests;

public sealed class PersistenceConfigurationTests
{
    [Fact]
    public void Missing_connection_string_fails_fast()
    {
        var configuration = new ConfigurationBuilder().Build();

        var exception = Assert.Throws<InvalidOperationException>(
            () => new ServiceCollection().AddWeaponsOfOrderPersistence(configuration));

        Assert.Contains("WeaponsOfOrder", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Configured_connection_string_resolves_a_npgsql_context()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:WeaponsOfOrder"] =
                    "Host=localhost;Port=5433;Database=weapons_of_order_tests;Username=woo_dev;Password=woo_dev",
            })
            .Build();

        using var provider = new ServiceCollection()
            .AddWeaponsOfOrderPersistence(configuration)
            .BuildServiceProvider();

        using var scope = provider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<WeaponsOfOrderDbContext>();

        Assert.Equal("Npgsql.EntityFrameworkCore.PostgreSQL", context.Database.ProviderName);
    }
}
