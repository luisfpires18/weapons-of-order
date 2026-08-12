namespace WeaponsOfOrder.Api.Preparation;

/// <summary>
/// A preparation request the server will not carry out, carrying the code the interface
/// switches on.
/// </summary>
internal sealed class PreparationRejectedException(int statusCode, string code, string title, string detail)
    : Exception(detail)
{
    public int StatusCode { get; } = statusCode;

    public string Code { get; } = code;

    public string Title { get; } = title;

    public string Detail { get; } = detail;
}

/// <summary>
/// The error vocabulary of the inventory and Units API. Codes are stable; the prose is UI copy.
/// </summary>
/// <remarks>
/// A Unit or item that the caller does not own is reported exactly as one that does not exist.
/// The distinction would answer a question the caller has no business asking, and a guessed
/// identifier would otherwise tell an attacker which guesses were real.
/// </remarks>
internal static class PreparationProblems
{
    public const string UnitNotFoundCode = "unit_not_found";
    public const string ItemNotFoundCode = "item_not_found";
    public const string ItemNotEquippableCode = "item_not_equippable";
    public const string ItemAlreadyEquippedCode = "item_already_equipped";
    public const string ItemNotEquippedCode = "item_not_equipped";
    public const string SlotOccupiedCode = "unit_slot_occupied";
    public const string TwoHandedCode = "weapon_needs_both_hands";
    public const string UnknownSlotCode = "unknown_slot";
    public const string ConflictCode = "preparation_conflict";
    public const string MissingDefinitionCode = "unit_definition_missing";

    public static PreparationRejectedException UnitNotFound() => new(
        StatusCodes.Status404NotFound,
        UnitNotFoundCode,
        "No such unit.",
        "That unit is not one of yours. Reload the page to see your roster.");

    public static PreparationRejectedException ItemNotFound() => new(
        StatusCodes.Status404NotFound,
        ItemNotFoundCode,
        "No such item.",
        "That item is not one of yours. Reload the page to see what you own.");

    public static PreparationRejectedException ItemNotEquippable(string weaponType) => new(
        StatusCodes.Status409Conflict,
        ItemNotEquippableCode,
        "That cannot be equipped yet.",
        $"No wield data is authored for a {weaponType}, so the game does not know how it is held.");

    public static PreparationRejectedException ItemAlreadyEquipped() => new(
        StatusCodes.Status409Conflict,
        ItemAlreadyEquippedCode,
        "That weapon is already in use.",
        "It is in another unit's hands. Unequip it there first — there is only one of it.");

    public static PreparationRejectedException ItemNotEquipped() => new(
        StatusCodes.Status409Conflict,
        ItemNotEquippedCode,
        "That weapon is not in this unit's hands.",
        "Reload the page to see the current loadout.");

    public static PreparationRejectedException SlotOccupied() => new(
        StatusCodes.Status409Conflict,
        SlotOccupiedCode,
        "That hand is full.",
        "Unequip what the unit is already holding before putting something else there.");

    public static PreparationRejectedException NeedsBothHands() => new(
        StatusCodes.Status409Conflict,
        TwoHandedCode,
        "That weapon takes both hands.",
        "It occupies the whole loadout and cannot be assigned to one hand.");

    public static PreparationRejectedException UnknownSlot(int slots) => new(
        StatusCodes.Status400BadRequest,
        UnknownSlotCode,
        "No such slot.",
        $"A unit has {slots} weapon slots, numbered 1 to {slots}.");

    /// <summary>Two requests reached one loadout at once and the database refused the second.</summary>
    public static PreparationRejectedException Conflict() => new(
        StatusCodes.Status409Conflict,
        ConflictCode,
        "The loadout moved on.",
        "That action arrived twice, or after the loadout had already changed. Reload the page.");

    /// <summary>
    /// A player-owned Unit points at a definition key the content file no longer has.
    /// </summary>
    /// <remarks>
    /// Loud on purpose, and never resolved to some other definition. The row is a real Unit
    /// somebody owns; quietly showing it as a different one would be worse than an error, and
    /// silently hiding it would look like the Unit had been taken away.
    /// </remarks>
    public static PreparationRejectedException MissingDefinition(string definitionKey) => new(
        StatusCodes.Status500InternalServerError,
        MissingDefinitionCode,
        "Unit content is out of step with saved units.",
        $"No definition is authored for '{definitionKey}', which a saved unit references. Restore "
        + "that key in content/units.json.");
}

/// <summary>Renders a <see cref="PreparationRejectedException"/> as a problem response.</summary>
internal sealed class PreparationRejectionFilter : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(
        EndpointFilterInvocationContext context,
        EndpointFilterDelegate next)
    {
        try
        {
            return await next(context);
        }
        catch (PreparationRejectedException rejection)
        {
            if (rejection.StatusCode >= StatusCodes.Status500InternalServerError)
            {
                context.HttpContext.RequestServices
                    .GetRequiredService<ILoggerFactory>()
                    .CreateLogger(typeof(PreparationRejectionFilter))
                    .LogError("{Code}: {Detail}", rejection.Code, rejection.Detail);
            }

            return Results.Problem(
                title: rejection.Title,
                detail: rejection.Detail,
                statusCode: rejection.StatusCode,
                extensions: new Dictionary<string, object?> { ["code"] = rejection.Code });
        }
    }
}
