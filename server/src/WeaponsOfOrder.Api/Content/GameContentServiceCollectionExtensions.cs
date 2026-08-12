using Microsoft.Extensions.Options;

namespace WeaponsOfOrder.Api.Content;

/// <summary>
/// Registers the creator-authored game content and the catalogues that read it.
/// </summary>
/// <remarks>
/// <c>ValidateOnStart</c> is the point of this being options rather than a file the first
/// caller happens to parse: content the creator has broken stops the application at startup
/// with the offending entry named, instead of surfacing as an odd screen later.
/// </remarks>
internal static class GameContentServiceCollectionExtensions
{
    public static IServiceCollection AddWeaponsOfOrderGameContent(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services
            .AddOptions<UnitContentOptions>()
            .Bind(configuration.GetSection(UnitContentOptions.SectionName))
            .ValidateOnStart();

        services.AddSingleton<IValidateOptions<UnitContentOptions>, UnitContentValidator>();

        services
            .AddOptions<WeaponContentOptions>()
            .Bind(configuration.GetSection(WeaponContentOptions.SectionName))
            .ValidateOnStart();

        services.AddSingleton<IValidateOptions<WeaponContentOptions>, WeaponContentValidator>();

        // Scoped, because they are built from IOptionsSnapshot: one consistent view of the
        // content per request, and a saved edit takes effect on the next one.
        services.AddScoped<UnitCatalogue>();
        services.AddScoped<WeaponCatalogue>();

        return services;
    }
}
