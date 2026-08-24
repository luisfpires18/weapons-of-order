using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace WeaponsOfOrder.Api.Battle;

/// <summary>
/// Composition seam for army deployment and battles. The host knows this method; it does not know
/// the combat rules, the simulator, or the persistence behind them.
/// </summary>
/// <remarks>
/// <c>ValidateOnStart</c> is the point of the tuning being options rather than constants: a battle
/// tuned into a state it cannot run in stops the application at startup with the offending setting
/// named, instead of surfacing as a battle that never comes back.
/// </remarks>
internal static class BattleServiceCollectionExtensions
{
    public static IServiceCollection AddWeaponsOfOrderBattle(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services
            .AddOptions<CombatOptions>()
            .Bind(configuration.GetSection(CombatOptions.SectionName))
            .ValidateOnStart();

        services.AddSingleton<IValidateOptions<CombatOptions>, CombatOptionsValidator>();

        // Shared with the forge and the preparation service, which register it the same way.
        services.TryAddSingleton(TimeProvider.System);

        // Scoped, because the profiles are built from IOptionsSnapshot: one consistent view of the
        // tuning per request, and a saved edit takes effect on the next one.
        services.AddScoped<CombatProfiles>();
        services.AddScoped<ArmyService>();
        services.AddScoped<BattleService>();

        return services;
    }
}
