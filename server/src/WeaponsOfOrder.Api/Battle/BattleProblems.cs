namespace WeaponsOfOrder.Api.Battle;

/// <summary>
/// A deployment or battle request the server will not carry out, carrying the code the interface
/// switches on.
/// </summary>
internal sealed class BattleRejectedException(int statusCode, string code, string title, string detail)
    : Exception(detail)
{
    public int StatusCode { get; } = statusCode;

    public string Code { get; } = code;

    public string Title { get; } = title;

    public string Detail { get; } = detail;
}

/// <summary>
/// The error vocabulary of the battle API. Codes are stable; the prose is UI copy.
/// </summary>
/// <remarks>
/// A Unit the caller does not own is reported exactly as one that does not exist. The distinction
/// would answer a question the caller has no business asking, and a guessed identifier would
/// otherwise tell an attacker which guesses were real.
/// </remarks>
internal static class BattleProblems
{
    public const string UnitNotFoundCode = "unit_not_found";
    public const string DuplicateUnitCode = "unit_deployed_twice";
    public const string HexOccupiedCode = "hex_occupied";
    public const string HexOutsideHalfCode = "hex_outside_deployment_half";
    public const string ActiveLimitCode = "active_limit";
    public const string ReserveLimitCode = "reserve_limit";
    public const string ArmyLimitCode = "army_limit";
    public const string EmptyArmyCode = "army_empty";
    public const string ConflictCode = "army_conflict";

    public static BattleRejectedException UnitNotFound() => new(
        StatusCodes.Status404NotFound,
        UnitNotFoundCode,
        "No such unit.",
        "That unit is not one of yours. Reload the page to see your roster.");

    public static BattleRejectedException DuplicateUnit() => new(
        StatusCodes.Status409Conflict,
        DuplicateUnitCode,
        "A unit can only be in one place.",
        "The same unit was placed twice. Reload the page to see the current deployment.");

    public static BattleRejectedException HexOccupied(int column, int row) => new(
        StatusCodes.Status409Conflict,
        HexOccupiedCode,
        "That hex is taken.",
        $"Two units were placed on column {column}, row {row}. Only one unit stands on a hex.");

    public static BattleRejectedException HexOutsideHalf(int column, int row, int columns, int rows) => new(
        StatusCodes.Status400BadRequest,
        HexOutsideHalfCode,
        "That hex is not yours to deploy on.",
        $"Column {column}, row {row} is outside your deployment half, which is the first {columns} "
        + $"columns of {rows} rows.");

    public static BattleRejectedException ActiveLimit(int limit) => new(
        StatusCodes.Status400BadRequest,
        ActiveLimitCode,
        "Too many units on the battlefield.",
        $"At most {limit} units may be deployed at once. The rest wait in reserve.");

    public static BattleRejectedException ReserveLimit(int limit) => new(
        StatusCodes.Status400BadRequest,
        ReserveLimitCode,
        "Too many units in reserve.",
        $"At most {limit} units may wait in reserve.");

    public static BattleRejectedException ArmyLimit(int limit) => new(
        StatusCodes.Status400BadRequest,
        ArmyLimitCode,
        "Too many units in the army.",
        $"An army brings at most {limit} units to a battle, deployed and reserve together.");

    /// <summary>Reserves alone are not an army: nobody would turn up to hold a slot open.</summary>
    public static BattleRejectedException EmptyArmy() => new(
        StatusCodes.Status409Conflict,
        EmptyArmyCode,
        "Nobody is deployed.",
        "Place at least one unit on the battlefield before starting a battle.");

    /// <summary>Two requests reached one army at once and the database refused the second.</summary>
    public static BattleRejectedException Conflict() => new(
        StatusCodes.Status409Conflict,
        ConflictCode,
        "The army moved on.",
        "That change arrived twice, or after the army had already changed. Reload the page.");
}

/// <summary>Renders a <see cref="BattleRejectedException"/> as a problem response.</summary>
internal sealed class BattleRejectionFilter : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(
        EndpointFilterInvocationContext context,
        EndpointFilterDelegate next)
    {
        try
        {
            return await next(context);
        }
        catch (BattleRejectedException rejection)
        {
            return Results.Problem(
                title: rejection.Title,
                detail: rejection.Detail,
                statusCode: rejection.StatusCode,
                extensions: new Dictionary<string, object?> { ["code"] = rejection.Code });
        }
    }
}
