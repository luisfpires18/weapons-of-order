using WeaponsOfOrder.Infrastructure.Gameplay;

namespace WeaponsOfOrder.Api.Forge;

/// <summary>
/// Every tunable number the ordinary forge uses, bound from the <c>Forge</c> configuration
/// section.
/// </summary>
/// <remarks>
/// <strong>These are temporary prototype balance values, not canon.</strong> The blacksmith
/// canon fixes the structure — Metal/Wood/Leather, the four heat bands, one Strike action,
/// Common/Rare/Epic, forgiving routine forging — and explicitly leaves costs, thresholds and
/// timings as balance data. They live here, and are mirrored in <c>appsettings.json</c>, so
/// tuning the forge is an edit to data rather than a hunt through endpoint and React code.
/// </remarks>
internal sealed class ForgeOptions
{
    public const string SectionName = "Forge";

    /// <summary>
    /// The ordinary weapon recipes the forge offers, keyed by a stable content key.
    /// </summary>
    /// <remarks>
    /// One entry, because Task 4 is one vertical slice. The catalogue is a dictionary rather
    /// than a hardcoded sword so adding the next weapon is authored data, and so the recipe
    /// key stays the identity while <see cref="ForgeRecipeSettings.DisplayName"/> stays copy.
    /// </remarks>
    public Dictionary<string, ForgeRecipeSettings> Recipes { get; set; } = new(StringComparer.Ordinal)
    {
        ["weapon.sword"] = new()
        {
            DisplayName = "Sword",
            WeaponType = "Sword",
            Metal = 3,
            Wood = 1,
            Leather = 0,
        },
    };

    public HeatSettings Heat { get; set; } = new();

    public StrikeSettings Strike { get; set; } = new();

    public CraftsmanshipSettings Craftsmanship { get; set; } = new();

    /// <summary>
    /// The opening material stock a player is granted the first time they open the forge.
    /// </summary>
    /// <remarks>
    /// <strong>Development bootstrap data.</strong> There is no gathering, production or
    /// economy yet, and something has to make the forge real enough to use. It is not a
    /// statement about what an account permanently begins with: when a real material source
    /// exists it replaces the grant in <c>ForgeService</c> without the forge itself changing.
    /// </remarks>
    public MaterialAmounts StartingMaterials { get; set; } = new() { Metal = 24, Wood = 12, Leather = 8 };
}

/// <summary>One authored ordinary-weapon recipe.</summary>
internal sealed class ForgeRecipeSettings
{
    /// <summary>What the interface calls it.</summary>
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>Canonical weapon type from the weapon registry.</summary>
    public string WeaponType { get; set; } = string.Empty;

    public int Metal { get; set; }

    public int Wood { get; set; }

    public int Leather { get; set; }

    public MaterialAmounts Cost => new() { Metal = Metal, Wood = Wood, Leather = Leather };
}

/// <summary>A quantity of each generic material. Used for both costs and stock.</summary>
internal sealed class MaterialAmounts
{
    public int Metal { get; set; }

    public int Wood { get; set; }

    public int Leather { get; set; }
}

/// <summary>
/// The temperature model: one scale, three boundaries, and the rates that move along it.
/// </summary>
/// <remarks>
/// The scale is abstract 0-100 rather than degrees. Degrees would invite a realism argument
/// the design explicitly does not want, and the player reads a gauge and a band name.
/// </remarks>
internal sealed class HeatSettings
{
    public double MaxTemperature { get; set; } = 100;

    /// <summary>Below this the workpiece is Cold.</summary>
    public double WorkableFrom { get; set; } = 40;

    public double IdealFrom { get; set; } = 65;

    /// <summary>At or above this the workpiece is Burning and the ruin clock runs.</summary>
    public double BurningFrom { get; set; } = 85;

    public double HeatRatePerSecond { get; set; } = 30;

    public double CoolRatePerSecond { get; set; } = 18;

    /// <summary>
    /// Heat the workpiece loses to each blow, which is what gives the loop its rhythm:
    /// heat, strike, heat again. Without it a player could reach Ideal once and then strike
    /// three times from the same heat.
    /// </summary>
    public double LossPerStrike { get; set; } = 22;

    /// <summary>
    /// Cumulative seconds in the Burning band before the workpiece is ruined. Cumulative,
    /// not consecutive: pulling it out for a moment does not reset the damage already done.
    /// A single too-hot strike costs quality and nothing else.
    /// </summary>
    public double BurnGraceSeconds { get; set; } = 3;
}

internal sealed class StrikeSettings
{
    /// <summary>
    /// Canon asks for only a few meaningful blows on an ordinary weapon.
    /// </summary>
    public int Required { get; set; } = 3;

    /// <summary>
    /// The shortest gap between two blows. It is a game rule — a hammer is not a mash
    /// button — and it is also what makes a double-submitted press cheap to reject before
    /// the database has to.
    /// </summary>
    public double CooldownSeconds { get; set; } = 0.35;
}

/// <summary>
/// How the strikes that landed become Common, Rare or Epic.
/// </summary>
/// <remarks>
/// Deliberately a small transparent sum rather than a rarity roll: canon says quality is
/// influenced by actual forging performance and must not be modelled as a disconnected
/// random draw. There is no randomness here at all, which also makes the rule testable.
/// </remarks>
internal sealed class CraftsmanshipSettings
{
    public int ColdPoints { get; set; }

    public int WorkablePoints { get; set; } = 1;

    public int IdealPoints { get; set; } = 2;

    /// <summary>Struck while burning: no credit, but the workpiece survives.</summary>
    public int BurningPoints { get; set; }

    /// <summary>
    /// With three strikes the ceiling is 6, so Epic wants near-perfect timing — two Ideal
    /// blows and one Workable will do it — while three Workable blows still reach Rare.
    /// Routine forging is meant to be forgiving.
    /// </summary>
    public int EpicScore { get; set; } = 5;

    public int RareScore { get; set; } = 3;

    public int PointsFor(HeatBand band) => band switch
    {
        HeatBand.Ideal => IdealPoints,
        HeatBand.Workable => WorkablePoints,
        HeatBand.Burning => BurningPoints,
        _ => ColdPoints,
    };
}
