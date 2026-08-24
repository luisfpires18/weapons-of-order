using System.Security.Claims;
using WeaponsOfOrder.Api.Preparation;
using WeaponsOfOrder.Api.Security;

namespace WeaponsOfOrder.Api.Battle;

/// <summary>
/// The army and battle API the browser client talks to.
/// </summary>
/// <remarks>
/// Three routes: read the army, replace the army, fight a battle with it. Deliberately thin —
/// every decision worth making is made below this file, and the point of the shape is what the
/// browser is <em>allowed to say</em>.
/// <para>
/// It may name its own Units and the hexes it wants them on. It may not name an owner, a stat, a
/// weapon's worth, an opponent, a seed, or a result. All of those are resolved from the session
/// cookie and the server's own content and configuration, which is what makes a battle the
/// server's answer rather than the browser's claim.
/// </para>
/// </remarks>
internal static class BattleEndpoints
{
    private const string RoutePrefix = "/api/battle";

    public static IEndpointRouteBuilder MapWeaponsOfOrderBattle(this IEndpointRouteBuilder endpoints)
    {
        var reads = endpoints
            .MapGroup(RoutePrefix)
            .AddEndpointFilter(new BattleRejectionFilter())
            .AddEndpointFilter(new PreparationRejectionFilter())
            .RequireAuthorization();

        reads.MapGet("/army", ReadArmyAsync);

        // Antiforgery from the group, authorization from the call below, ownership from the cookie —
        // the same shape the account, forge and preparation APIs established.
        var mutations = endpoints
            .MapMutations(RoutePrefix)
            .AddEndpointFilter(new BattleRejectionFilter())
            .AddEndpointFilter(new PreparationRejectionFilter())
            .RequireAuthorization();

        mutations.MapPost("/army", SaveArmyAsync);
        mutations.MapPost("/simulate", SimulateAsync);

        return endpoints;
    }

    private static async Task<IResult> ReadArmyAsync(
        ClaimsPrincipal principal,
        ArmyService army,
        CancellationToken cancellationToken)
        => Results.Ok((await army.ReadAsync(AccountId(principal), cancellationToken)).ToPayload());

    private static async Task<IResult> SaveArmyAsync(
        SaveArmyRequest request,
        ClaimsPrincipal principal,
        ArmyService army,
        CancellationToken cancellationToken)
        => Results.Ok((await army.SaveAsync(AccountId(principal), request, cancellationToken)).ToPayload());

    /// <summary>
    /// Resolves one battle against the training opposition.
    /// </summary>
    /// <remarks>
    /// A POST with no body. The army it fights is the one the account has saved, so there is
    /// nothing for the caller to send and nothing it could send that would be believed.
    /// </remarks>
    private static async Task<IResult> SimulateAsync(
        ClaimsPrincipal principal,
        BattleService battle,
        CancellationToken cancellationToken)
        => Results.Ok(await battle.SimulateAsync(AccountId(principal), cancellationToken));

    /// <summary>
    /// The account the cookie says this is. Authorization has already run, so the claim is present;
    /// an unparseable one would be a server fault rather than a caller's mistake.
    /// </summary>
    private static Guid AccountId(ClaimsPrincipal principal)
        => principal.GetAccountId()
            ?? throw new InvalidOperationException("An authorized request carried no usable account id.");
}
