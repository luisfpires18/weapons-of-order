using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace WeaponsOfOrder.Api.Forge;

/// <summary>
/// Composition seam for the forge. The host knows this method; it does not know the balance
/// values, the rules or the persistence behind them.
/// </summary>
internal static class ForgeServiceCollectionExtensions
{
    public static IServiceCollection AddWeaponsOfOrderForge(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services
            .AddOptions<ForgeOptions>()
            .Bind(configuration.GetSection(ForgeOptions.SectionName))
            .ValidateOnStart();

        services.AddSingleton<IValidateOptions<ForgeOptions>, ForgeOptionsValidator>();

        // The forge decides what a strike was worth from elapsed time, so time is injected
        // rather than read from DateTimeOffset.UtcNow. Tests drive it directly and never
        // wait for a real second to pass.
        services.TryAddSingleton(TimeProvider.System);

        services.AddScoped<ForgeService>();

        return services;
    }
}
