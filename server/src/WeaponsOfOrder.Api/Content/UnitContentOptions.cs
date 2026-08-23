namespace WeaponsOfOrder.Api.Content;

/// <summary>
/// The Unit catalogue as the creator wrote it, bound from <c>content/units.json</c>.
/// </summary>
/// <remarks>
/// This is content, not balance and not code. Every field below can be edited and reloaded
/// without a C# change, a React change or an EF migration, because a player-owned Unit stores
/// only <see cref="UnitDefinitionSettings.Key"/> and resolves the rest through here on each
/// read.
/// </remarks>
internal sealed class UnitContentOptions
{
    public const string SectionName = "UnitContent";

    /// <summary>
    /// The kingdoms a definition may belong to.
    /// </summary>
    /// <remarks>
    /// Authored rather than an enum because kingdom names are the creator's. Listing them is
    /// what turns a typo in a Unit's <c>Kingdom</c> into a startup failure instead of a new
    /// kingdom nobody meant to create.
    /// </remarks>
    public List<string> Kingdoms { get; set; } = [];

    public List<UnitDefinitionSettings> Units { get; set; } = [];
}

/// <summary>One authored Unit definition.</summary>
internal sealed class UnitDefinitionSettings
{
    /// <summary>
    /// Stable identity. Persistent player-owned rows reference it, so it must not be renamed
    /// once an account holds one; everything else here is free to change.
    /// </summary>
    public string Key { get; set; } = string.Empty;

    /// <summary>What the interface calls it. Copy, and only copy.</summary>
    public string DisplayName { get; set; } = string.Empty;

    /// <summary><c>Regular</c> or <c>Hero</c>.</summary>
    public string Type { get; set; } = string.Empty;

    public string Kingdom { get; set; } = string.Empty;

    /// <summary>Fixed classification tier, 1-3. Not an upgrade level.</summary>
    public int Tier { get; set; }

    /// <summary><c>Light</c>, <c>Medium</c> or <c>Heavy</c>.</summary>
    public string MaxArmor { get; set; } = string.Empty;

    /// <summary>
    /// The one field here with structural meaning. Combat reads it; nothing reads a display
    /// name, and no weapon is restricted by either.
    /// </summary>
    /// <remarks>
    /// Text rather than <see cref="bool"/>, and parsed by <see cref="ContentFlag"/>. The
    /// configuration binder does not fail on a value it cannot convert — it leaves the
    /// property at its default — so a typed flag would turn <c>"Mounted": "sometimes"</c> into
    /// a unit quietly on foot. Creator content is not repaired quietly.
    /// </remarks>
    public string? Mounted { get; set; }

    /// <summary>Grant every account exactly one of these, once. Absent means no.</summary>
    public string? Starter { get; set; }
}

/// <summary>
/// Reads a yes/no value out of content without ever guessing at one.
/// </summary>
internal static class ContentFlag
{
    /// <summary>Whether <paramref name="value"/> is a flag this content model accepts.</summary>
    /// <remarks>
    /// <paramref name="required"/> separates a field that must be stated from one whose
    /// absence is itself an answer.
    /// </remarks>
    public static bool IsValid(string? value, bool required)
        => string.IsNullOrWhiteSpace(value) ? !required : bool.TryParse(value.Trim(), out _);

    /// <summary>The flag's value, defaulting to <see langword="false"/> when it is absent.</summary>
    public static bool Read(string? value)
        => !string.IsNullOrWhiteSpace(value) && bool.Parse(value.Trim());
}
