using System.Security.Claims;
using Microsoft.AspNetCore.Antiforgery;

namespace WeaponsOfOrder.Api.Security;

/// <summary>
/// The security conventions every Weapons of Order API endpoint is built from.
/// </summary>
/// <remarks>
/// Future gameplay endpoints follow this same pattern: put mutations in a group created by
/// <see cref="MapMutations"/>, call <c>RequireAuthorization()</c> when the endpoint acts on
/// behalf of an account, and resolve the acting account with <see cref="GetAccountId"/> —
/// never from the request body.
/// </remarks>
internal static class ApiSecurity
{
    /// <summary>
    /// Header the browser client echoes the antiforgery request token in. Cookie
    /// authentication means a cross-site page can make the browser send the session
    /// cookie; it cannot read this token out of a same-origin JSON response.
    /// </summary>
    public const string AntiforgeryHeaderName = "X-WoO-Antiforgery";

    /// <summary>
    /// Creates a route group whose every endpoint has server-validated antiforgery.
    /// </summary>
    /// <remarks>
    /// The filter is applied to the group rather than to individual endpoints so that
    /// adding a mutation cannot accidentally opt out of CSRF protection. Unauthenticated
    /// mutations are covered too: login CSRF (forcing a victim into an attacker's session)
    /// is a real attack, and one uniform rule is easier to keep correct than an exemption
    /// list.
    /// </remarks>
    public static RouteGroupBuilder MapMutations(this IEndpointRouteBuilder endpoints, string prefix)
        => endpoints.MapGroup(prefix).AddEndpointFilter(new AntiforgeryEndpointFilter());

    /// <summary>
    /// The account id of the caller, taken from the authentication cookie the server issued.
    /// </summary>
    public static Guid? GetAccountId(this ClaimsPrincipal principal)
    {
        var value = principal.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(value, out var id) ? id : null;
    }
}

/// <summary>
/// Validates the antiforgery request token on a mutating endpoint.
/// </summary>
/// <remarks>
/// ASP.NET Core's antiforgery middleware only validates endpoints that bind form data, so
/// a JSON API needs this explicit check. The failure is reported as a plain 400 with a
/// machine-readable code and no detail: the client needs to know it must refresh its
/// token, and nothing else about why validation failed is safe to publish.
/// </remarks>
internal sealed class AntiforgeryEndpointFilter : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(
        EndpointFilterInvocationContext context,
        EndpointFilterDelegate next)
    {
        var antiforgery = context.HttpContext.RequestServices.GetRequiredService<IAntiforgery>();

        try
        {
            await antiforgery.ValidateRequestAsync(context.HttpContext);
        }
        catch (AntiforgeryValidationException)
        {
            return Results.Problem(
                title: "Antiforgery validation failed.",
                detail: "Reload the page and try again.",
                statusCode: StatusCodes.Status400BadRequest,
                extensions: new Dictionary<string, object?> { ["code"] = "antiforgery" });
        }

        return await next(context);
    }
}
