using System.Security.Claims;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using SekaiPlatform.Shared.Web.Auth;
using SekaiPlatform.Shared.Web.Context;
using SekaiPlatform.Shared.Web.Responses;

namespace SekaiPlatform.SearchService.Search;

/// <summary>
/// Maps Search Service internal endpoints for index maintenance.
/// </summary>
internal static class SearchIndexEndpoints
{
    /// <summary>
    /// Registers the internal search index rebuild endpoint.
    /// </summary>
    public static IEndpointRouteBuilder MapSearchIndexEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/internal/search/index/rebuild", async Task<IResult> (
            SearchIndexRebuildRequest? request,
            SearchIndexRebuilder rebuilder,
            ICurrentRequestContextAccessor contextAccessor,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            request ??= new SearchIndexRebuildRequest();
            var scope = SearchIndexRebuilder.NormalizeScope(request.Scope);
            if (scope is not SearchIndexConstants.ScopeAll
                and not SearchIndexConstants.ScopeSource
                and not SearchIndexConstants.ScopeTranslation)
            {
                return Error(contextAccessor, StatusCodes.Status400BadRequest, "Unsupported search index rebuild scope.");
            }

            var authorizationError = ValidateInternalAuthorization(httpContext, contextAccessor.GetCurrent(), request, scope);
            if (authorizationError is not null)
            {
                return Error(contextAccessor, StatusCodes.Status403Forbidden, authorizationError);
            }

            var validationError = ValidateRequest(request, scope);
            if (validationError is not null)
            {
                return Error(contextAccessor, StatusCodes.Status400BadRequest, validationError);
            }

            var response = await rebuilder.RebuildAsync(request, cancellationToken);
            return Results.Json(response);
        }).RequireAuthorization(policy =>
        {
            policy.AuthenticationSchemes.Add(JwtBearerDefaults.AuthenticationScheme);
            policy.RequireAuthenticatedUser();
        });

        return app;
    }

    /// <summary>
    /// Validates rebuild filters whose combinations affect index deletion semantics.
    /// </summary>
    private static string? ValidateRequest(SearchIndexRebuildRequest request, string scope)
    {
        var hasStoryIds = request.StoryIds is { Length: > 0 };
        var hasTranslationVersionIds = request.TranslationVersionIds is { Length: > 0 };
        var hasTranslationFilter = request.TenantId is not null
            || request.TranslationVersionId is not null
            || hasTranslationVersionIds;
        if (request.ForceRecreate
            && (scope != SearchIndexConstants.ScopeAll || hasStoryIds || hasTranslationFilter))
        {
            return "Force recreate only supports a full all-scope rebuild without filters.";
        }

        if (request.TranslationVersionId is not null && hasTranslationVersionIds)
        {
            return "Translation index rebuild does not support both single and multiple translation version filters.";
        }

        if (scope == SearchIndexConstants.ScopeSource && hasTranslationFilter)
        {
            return "Source index rebuild does not support tenant or translation version filters.";
        }

        if (scope == SearchIndexConstants.ScopeAll && hasTranslationFilter)
        {
            return "All-scope index rebuild only supports story filters.";
        }

        return null;
    }

    /// <summary>
    /// Validates internal token actor, scope, and tenant binding for index maintenance requests.
    /// </summary>
    private static string? ValidateInternalAuthorization(
        HttpContext httpContext,
        CurrentRequestContext requestContext,
        SearchIndexRebuildRequest request,
        string rebuildScope)
    {
        var actor = httpContext.User.FindFirstValue(SekaiInternalAuthDefaults.ActorClaimType);
        var tokenScope = httpContext.User.FindFirstValue(SekaiInternalAuthDefaults.ScopeClaimType);
        if (tokenScope == SekaiInternalAuthDefaults.SearchIndexRebuildScope)
        {
            return actor is SekaiInternalAuthDefaults.AssetServiceActor or SekaiInternalAuthDefaults.SyncWorkerActor
                ? null
                : "Forbidden.";
        }

        if (tokenScope != SekaiInternalAuthDefaults.SearchTranslationRefreshScope
            || actor != SekaiInternalAuthDefaults.AssetServiceActor)
        {
            return "Forbidden.";
        }

        if (requestContext.TenantId is null)
        {
            return "Forbidden.";
        }

        if (rebuildScope != SearchIndexConstants.ScopeTranslation)
        {
            return "Translation refresh only supports translation scope.";
        }

        if (request.TenantId is null)
        {
            request.TenantId = requestContext.TenantId.Value;
            return null;
        }

        return request.TenantId.Value == requestContext.TenantId.Value
            ? null
            : "Forbidden.";
    }

    /// <summary>
    /// Creates a trace-aware error response for search index maintenance endpoints.
    /// </summary>
    private static IResult Error(
        ICurrentRequestContextAccessor contextAccessor,
        int statusCode,
        string message)
    {
        return Results.Json(
            new ErrorResponse(message, contextAccessor.GetCurrent().TraceId),
            statusCode: statusCode);
    }
}
