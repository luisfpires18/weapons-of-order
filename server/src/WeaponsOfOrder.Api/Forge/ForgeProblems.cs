namespace WeaponsOfOrder.Api.Forge;

/// <summary>
/// A forge request the server will not carry out, carrying the code the interface switches on.
/// </summary>
/// <remarks>
/// An exception rather than a result type because every one of these is the end of the
/// request: the endpoints stay a single expression each, and no caller can forget to check.
/// <see cref="ForgeRejectionFilter"/> turns it into <c>application/problem+json</c>.
/// </remarks>
internal sealed class ForgeRejectedException(int statusCode, string code, string title, string detail)
    : Exception(detail)
{
    public int StatusCode { get; } = statusCode;

    public string Code { get; } = code;

    public string Title { get; } = title;

    public string Detail { get; } = detail;
}

/// <summary>
/// The error vocabulary of the forge API. Codes are stable; the prose is UI copy.
/// </summary>
internal static class ForgeProblems
{
    public const string UnknownRecipeCode = "forge_unknown_recipe";
    public const string InsufficientMaterialsCode = "forge_insufficient_materials";
    public const string InProgressCode = "forge_in_progress";
    public const string NoActiveForgeCode = "forge_not_active";
    public const string StrikeCooldownCode = "forge_strike_cooldown";
    public const string ConflictCode = "forge_conflict";

    public static ForgeRejectedException UnknownRecipe() => new(
        StatusCodes.Status400BadRequest,
        UnknownRecipeCode,
        "No such recipe.",
        "The forge does not have that recipe. Reload the page to see what it can make.");

    public static ForgeRejectedException InsufficientMaterials(string name, MaterialsPayload cost) => new(
        StatusCodes.Status409Conflict,
        InsufficientMaterialsCode,
        "Not enough materials.",
        $"A {name} needs {Describe(cost)}. Your stock does not cover it.");

    public static ForgeRejectedException InProgress() => new(
        StatusCodes.Status409Conflict,
        InProgressCode,
        "A workpiece is already on the anvil.",
        "Finish or set aside the current workpiece before starting another.");

    public static ForgeRejectedException NoActiveForge() => new(
        StatusCodes.Status409Conflict,
        NoActiveForgeCode,
        "Nothing is on the anvil.",
        "Start a forge before heating or striking.");

    public static ForgeRejectedException StrikeCooldown() => new(
        StatusCodes.Status409Conflict,
        StrikeCooldownCode,
        "Too soon after the last blow.",
        "Let the hammer come back up before you strike again.");

    /// <summary>
    /// Two requests reached the same workpiece at once and the database refused the second.
    /// The caller re-reads the forge rather than being told what the other request did.
    /// </summary>
    public static ForgeRejectedException Conflict() => new(
        StatusCodes.Status409Conflict,
        ConflictCode,
        "The forge moved on.",
        "That action arrived twice, or after the workpiece had already changed. Reload the forge.");

    private static string Describe(MaterialsPayload cost)
    {
        var parts = new List<string>(3);

        if (cost.Metal > 0)
        {
            parts.Add($"{cost.Metal} Metal");
        }

        if (cost.Wood > 0)
        {
            parts.Add($"{cost.Wood} Wood");
        }

        if (cost.Leather > 0)
        {
            parts.Add($"{cost.Leather} Leather");
        }

        return parts.Count == 0 ? "no materials" : string.Join(", ", parts);
    }
}

/// <summary>Renders a <see cref="ForgeRejectedException"/> as a problem response.</summary>
internal sealed class ForgeRejectionFilter : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(
        EndpointFilterInvocationContext context,
        EndpointFilterDelegate next)
    {
        try
        {
            return await next(context);
        }
        catch (ForgeRejectedException rejection)
        {
            return Results.Problem(
                title: rejection.Title,
                detail: rejection.Detail,
                statusCode: rejection.StatusCode,
                extensions: new Dictionary<string, object?> { ["code"] = rejection.Code });
        }
    }
}
