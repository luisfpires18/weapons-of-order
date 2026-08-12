using System.Security.Claims;
using WeaponsOfOrder.Api.Security;

namespace WeaponsOfOrder.Api.Preparation;

/// <summary>
/// The inventory and Units API the browser client talks to.
/// </summary>
/// <remarks>
/// Four routes, each one a thing the player looked at or did. Deliberately not a single
/// game-state endpoint: authorization, antiforgery and rate limiting mean something per action
/// and nothing per screen.
/// <para>
/// Every route requires a session and resolves the acting account from the authentication
/// cookie. A Unit id in the path is a claim, not a credential — it is checked against that
/// account, and a Unit belonging to somebody else is answered as one that does not exist.
/// </para>
/// </remarks>
internal static class PreparationEndpoints
{
    private const string RoutePrefix = "/api";

    public static IEndpointRouteBuilder MapWeaponsOfOrderPreparation(this IEndpointRouteBuilder endpoints)
    {
        var reads = endpoints
            .MapGroup(RoutePrefix)
            .AddEndpointFilter(new PreparationRejectionFilter())
            .RequireAuthorization();

        reads.MapGet("/inventory/items", ListInventoryAsync);
        reads.MapGet("/units", ListUnitsAsync);

        // Antiforgery from the group, authorization from the call below, ownership from the
        // cookie — the same shape the account and forge APIs established.
        var mutations = endpoints
            .MapMutations(RoutePrefix)
            .AddEndpointFilter(new PreparationRejectionFilter())
            .RequireAuthorization();

        mutations.MapPost("/units/{unitId:guid}/equip", EquipAsync);
        mutations.MapPost("/units/{unitId:guid}/unequip", UnequipAsync);

        return endpoints;
    }

    private static async Task<IResult> ListInventoryAsync(
        ClaimsPrincipal principal,
        PreparationService preparation,
        CancellationToken cancellationToken)
        => Results.Ok(await preparation.ListInventoryAsync(AccountId(principal), cancellationToken));

    private static async Task<IResult> ListUnitsAsync(
        ClaimsPrincipal principal,
        PreparationService preparation,
        CancellationToken cancellationToken)
        => Results.Ok(await preparation.ListUnitsAsync(AccountId(principal), cancellationToken));

    private static async Task<IResult> EquipAsync(
        Guid unitId,
        EquipRequest request,
        ClaimsPrincipal principal,
        PreparationService preparation,
        CancellationToken cancellationToken)
        => Results.Ok(await preparation.EquipAsync(
            AccountId(principal),
            unitId,
            request.ItemId,
            request.Slot,
            cancellationToken));

    private static async Task<IResult> UnequipAsync(
        Guid unitId,
        UnequipRequest request,
        ClaimsPrincipal principal,
        PreparationService preparation,
        CancellationToken cancellationToken)
        => Results.Ok(await preparation.UnequipAsync(
            AccountId(principal),
            unitId,
            request.ItemId,
            cancellationToken));

    /// <summary>
    /// The account the cookie says this is. Authorization has already run, so the claim is
    /// present; an unparseable one would be a server fault rather than a caller's mistake.
    /// </summary>
    private static Guid AccountId(ClaimsPrincipal principal)
        => principal.GetAccountId()
            ?? throw new InvalidOperationException("An authorized request carried no usable account id.");
}
