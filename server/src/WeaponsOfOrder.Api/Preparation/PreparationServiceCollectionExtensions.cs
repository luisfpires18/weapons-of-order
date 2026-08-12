using Microsoft.Extensions.DependencyInjection.Extensions;

namespace WeaponsOfOrder.Api.Preparation;

/// <summary>
/// Composition seam for inventory, Units and equipment. The host knows this method; it does
/// not know the loadout rules or the persistence behind them.
/// </summary>
internal static class PreparationServiceCollectionExtensions
{
    public static IServiceCollection AddWeaponsOfOrderPreparation(this IServiceCollection services)
    {
        // Shared with the forge, which registers it the same way. Equipment records when it
        // happened, and a test should not have to wait for a real clock to prove it.
        services.TryAddSingleton(TimeProvider.System);

        services.AddScoped<PreparationService>();

        return services;
    }
}
