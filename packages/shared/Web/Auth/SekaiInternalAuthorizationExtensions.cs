using System.Security.Claims;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;

namespace SekaiPlatform.Shared.Web.Auth;

/// <summary>
/// Adds endpoint-level authorization requirements for internal service calls.
/// </summary>
public static class SekaiInternalAuthorizationExtensions
{
    /// <summary>
    /// Requires an authenticated internal token with the expected actor and scope.
    /// </summary>
    /// <param name="builder">Route handler builder to secure.</param>
    /// <param name="scope">Required internal capability.</param>
    /// <param name="allowedActors">Actors allowed to call the endpoint.</param>
    /// <param name="requireSubject">Whether a delegated user identifier is required.</param>
    /// <param name="requireTenant">Whether a delegated tenant identifier is required.</param>
    /// <returns>The same route handler builder for chaining.</returns>
    public static RouteHandlerBuilder RequireInternalAuthorization(
        this RouteHandlerBuilder builder,
        string scope,
        string[] allowedActors,
        bool requireSubject = false,
        bool requireTenant = false)
    {
        return builder.RequireAuthorization(policy =>
        {
            policy.AuthenticationSchemes.Add(JwtBearerDefaults.AuthenticationScheme);
            policy.RequireAuthenticatedUser();
            policy.RequireClaim(SekaiInternalAuthDefaults.ScopeClaimType, scope);
            policy.RequireAssertion(context => HasAllowedActor(context.User, allowedActors));

            if (requireSubject)
            {
                policy.RequireClaim(SekaiInternalAuthDefaults.SubjectUserIdClaimType);
            }

            if (requireTenant)
            {
                policy.RequireClaim(SekaiAuthDefaults.TenantIdClaimType);
            }
        });
    }

    /// <summary>
    /// Gets a claim value while accounting for JWT handler claim type mappings.
    /// </summary>
    internal static string? FindClaimValue(this ClaimsPrincipal principal, string claimType)
    {
        return principal.FindFirstValue(claimType);
    }

    private static bool HasAllowedActor(ClaimsPrincipal user, string[] allowedActors)
    {
        var actor = user.FindClaimValue(SekaiInternalAuthDefaults.ActorClaimType);
        return actor is not null && allowedActors.Contains(actor, StringComparer.Ordinal);
    }
}
