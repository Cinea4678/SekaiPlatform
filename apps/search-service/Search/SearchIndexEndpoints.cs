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

            var validationError = ValidateRequest(request, scope);
            if (validationError is not null)
            {
                return Error(contextAccessor, StatusCodes.Status400BadRequest, validationError);
            }

            var response = await rebuilder.RebuildAsync(request, cancellationToken);
            return Results.Json(response);
        }).RequireInternalAuthorization(
            SekaiInternalAuthDefaults.SearchIndexRebuildScope,
            [SekaiInternalAuthDefaults.AssetServiceActor, SekaiInternalAuthDefaults.SyncWorkerActor]);

        return app;
    }

    /// <summary>
    /// Validates rebuild filters whose combinations affect index deletion semantics.
    /// </summary>
    private static string? ValidateRequest(SearchIndexRebuildRequest request, string scope)
    {
        var hasStoryIds = request.StoryIds is { Length: > 0 };
        var hasTranslationFilter = request.TenantId is not null || request.TranslationVersionId is not null;
        if (request.ForceRecreate
            && (scope != SearchIndexConstants.ScopeAll || hasStoryIds || hasTranslationFilter))
        {
            return "Force recreate only supports a full all-scope rebuild without filters.";
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
