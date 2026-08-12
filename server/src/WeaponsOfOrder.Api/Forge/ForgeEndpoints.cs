using System.Security.Claims;
using WeaponsOfOrder.Api.Security;

namespace WeaponsOfOrder.Api.Forge;

/// <summary>
/// The forge API the browser client talks to.
/// </summary>
/// <remarks>
/// Small and explicit rather than one general game-state endpoint: each route is a thing the
/// player did, which is what makes antiforgery, authorization and rate limiting mean
/// something per action instead of per screen.
/// <para>
/// Every route requires a session, and every one of them resolves the acting account from
/// the authentication cookie. No request body carries an owner.
/// </para>
/// </remarks>
internal static class ForgeEndpoints
{
    private const string RoutePrefix = "/api/forge";

    public static IEndpointRouteBuilder MapWeaponsOfOrderForge(this IEndpointRouteBuilder endpoints)
    {
        var reads = endpoints
            .MapGroup(RoutePrefix)
            .AddEndpointFilter(new ForgeRejectionFilter())
            .RequireAuthorization();

        reads.MapGet("/state", GetStateAsync);
        reads.MapGet("/items", ListItemsAsync);

        // Antiforgery from the group, authorization from the call below, ownership from the
        // cookie — the same shape the account API established.
        var mutations = endpoints
            .MapMutations(RoutePrefix)
            .AddEndpointFilter(new ForgeRejectionFilter())
            .RequireAuthorization();

        mutations.MapPost("/begin", BeginAsync);
        mutations.MapPost("/heat", SetHeatingAsync);
        mutations.MapPost("/strike", StrikeAsync);
        mutations.MapPost("/abandon", AbandonAsync);

        return endpoints;
    }

    private static async Task<IResult> GetStateAsync(
        ClaimsPrincipal principal,
        ForgeService forge,
        CancellationToken cancellationToken)
        => Results.Ok(await forge.GetStateAsync(AccountId(principal), cancellationToken));

    private static async Task<IResult> ListItemsAsync(
        ClaimsPrincipal principal,
        ForgeService forge,
        CancellationToken cancellationToken)
        => Results.Ok(await forge.ListItemsAsync(AccountId(principal), cancellationToken));

    private static async Task<IResult> BeginAsync(
        BeginForgeRequest request,
        ClaimsPrincipal principal,
        ForgeService forge,
        CancellationToken cancellationToken)
        => Results.Ok(await forge.BeginAsync(AccountId(principal), request.RecipeKey, cancellationToken));

    private static async Task<IResult> SetHeatingAsync(
        HeatRequest request,
        ClaimsPrincipal principal,
        ForgeService forge,
        CancellationToken cancellationToken)
        => Results.Ok(await forge.SetHeatingAsync(AccountId(principal), request.Heating, cancellationToken));

    private static async Task<IResult> StrikeAsync(
        ClaimsPrincipal principal,
        ForgeService forge,
        CancellationToken cancellationToken)
        => Results.Ok(await forge.StrikeAsync(AccountId(principal), cancellationToken));

    private static async Task<IResult> AbandonAsync(
        ClaimsPrincipal principal,
        ForgeService forge,
        CancellationToken cancellationToken)
        => Results.Ok(await forge.AbandonAsync(AccountId(principal), cancellationToken));

    /// <summary>
    /// The account the cookie says this is. Authorization has already run, so the claim is
    /// present; an unparseable one would be a server fault rather than a caller's mistake.
    /// </summary>
    private static Guid AccountId(ClaimsPrincipal principal)
        => principal.GetAccountId()
            ?? throw new InvalidOperationException("An authorized request carried no usable account id.");
}
