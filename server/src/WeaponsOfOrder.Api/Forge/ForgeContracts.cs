namespace WeaponsOfOrder.Api.Forge;

/// <summary>A quantity of each generic crafting material.</summary>
internal sealed record MaterialsPayload(int Metal, int Wood, int Leather);

/// <summary>An ordinary weapon the player can forge, and whether they can afford it now.</summary>
internal sealed record RecipePayload(
    string Key,
    string Name,
    string WeaponType,
    MaterialsPayload Cost,
    bool Affordable);

/// <summary>One landed blow, reported as the band the server measured and nothing else.</summary>
/// <remarks>
/// No score, no temperature. The player needs to know the blow was Ideal; the points it was
/// worth are an implementation detail of the quality rule and publishing them would invite
/// the interface to start doing arithmetic the server owns.
/// </remarks>
internal sealed record StrikePayload(int Ordinal, string Band);

/// <summary>
/// The workpiece as of <paramref name="ObservedAt"/>.
/// </summary>
/// <remarks>
/// <paramref name="Temperature"/> and <paramref name="Heating"/> are an anchor, not a live
/// reading: together with the tuning block they let the client draw a smooth gauge by
/// running the same arithmetic the server does. The client's copy is decoration. Every
/// decision — what band a strike landed in, whether the piece burned, what it became — is
/// made from the server's own clock.
/// </remarks>
internal sealed record ForgeSessionPayload(
    Guid Id,
    string RecipeKey,
    string Name,
    string WeaponType,
    string Status,
    double Temperature,
    string Band,
    bool Heating,
    DateTimeOffset ObservedAt,
    int StrikesTaken,
    int StrikesRequired,
    IReadOnlyList<StrikePayload> Strikes,
    double BurnSeconds,
    string? Craftsmanship,
    Guid? ItemId);

/// <summary>
/// The balance values the client needs to animate the gauge and label its bands.
/// </summary>
/// <remarks>
/// Published rather than duplicated in the React bundle so that tuning the forge in
/// configuration moves both sides at once, and the gauge can never disagree with the rule
/// it is drawing.
/// </remarks>
internal sealed record ForgeTuningPayload(
    double MaxTemperature,
    double WorkableFrom,
    double IdealFrom,
    double BurningFrom,
    double HeatRatePerSecond,
    double CoolRatePerSecond,
    double BurnGraceSeconds,
    double StrikeCooldownSeconds);

/// <summary>Everything the forge screen needs in one read.</summary>
internal sealed record ForgeStatePayload(
    MaterialsPayload Materials,
    IReadOnlyList<RecipePayload> Recipes,
    ForgeSessionPayload? Session,
    ForgeTuningPayload Tuning);

internal sealed record ForgedItemPayload(
    Guid Id,
    string RecipeKey,
    string Name,
    string WeaponType,
    string Craftsmanship,
    string Origin,
    DateTimeOffset ForgedAt);

/// <summary>
/// The recipe to forge. Nothing else: the owner comes from the session cookie, and the
/// result comes from what the player then does at the anvil.
/// </summary>
internal sealed record BeginForgeRequest(string? RecipeKey);

/// <summary>Whether the player is holding the workpiece in the fire.</summary>
internal sealed record HeatRequest(bool Heating);
