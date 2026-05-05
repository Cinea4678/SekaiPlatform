using Microsoft.EntityFrameworkCore;
using SekaiPlatform.Database;
using SekaiPlatform.Shared.Web.Auth;
using SekaiPlatform.Shared.Web.Context;
using SekaiPlatform.Shared.Web.Responses;

namespace SekaiPlatform.SearchService.Search;

/// <summary>
/// Maps Search Service internal endpoints for tenant-scoped search queries.
/// </summary>
internal static class SearchQueryEndpoints
{
    private const int DefaultPage = 1;
    private const int DefaultPageSize = 20;
    private const int MaxPageSize = 100;
    private const int MaxResultWindow = 10_000;

    /// <summary>
    /// Registers the internal language asset search endpoint.
    /// </summary>
    public static IEndpointRouteBuilder MapSearchQueryEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/internal/search", async Task<IResult> (
            HttpContext httpContext,
            SekaiPlatformDbContext dbContext,
            ElasticsearchSearchClient searchClient,
            ICurrentRequestContextAccessor contextAccessor,
            CancellationToken cancellationToken) =>
        {
            var keyword = httpContext.Request.Query["keyword"].ToString().Trim();
            if (string.IsNullOrWhiteSpace(keyword))
            {
                return Error(contextAccessor, StatusCodes.Status400BadRequest, "搜索关键词不能为空。");
            }

            if (!TryReadPositiveInt(httpContext, "page", DefaultPage, out var page)
                || !TryReadPositiveInt(httpContext, "page_size", DefaultPageSize, out var pageSize))
            {
                return Error(contextAccessor, StatusCodes.Status400BadRequest, "分页参数无效。");
            }

            if (pageSize > MaxPageSize)
            {
                return Error(contextAccessor, StatusCodes.Status400BadRequest, "分页参数无效。");
            }

            if (((long)page - 1) * pageSize + pageSize > MaxResultWindow)
            {
                return Error(contextAccessor, StatusCodes.Status400BadRequest, "搜索分页超过最大结果窗口。");
            }

            var requestContext = contextAccessor.GetCurrent();
            if (requestContext.TenantId is null)
            {
                return Error(contextAccessor, StatusCodes.Status403Forbidden, "当前租户不能为空。");
            }

            if (!await IsCurrentTenantMemberAsync(dbContext, requestContext, cancellationToken))
            {
                return Error(contextAccessor, StatusCodes.Status403Forbidden, "无权访问。");
            }

            var response = await searchClient.SearchAsync(
                new SearchQueryRequest(requestContext.TenantId.Value, keyword, page, pageSize),
                cancellationToken);
            return Results.Json(response);
        }).RequireInternalAuthorization(
            SekaiInternalAuthDefaults.SearchQueryScope,
            [SekaiInternalAuthDefaults.ApiServiceActor],
            requireSubject: true,
            requireTenant: true);

        return app;
    }

    /// <summary>
    /// Checks whether the delegated user still has active access to the selected tenant.
    /// </summary>
    private static async Task<bool> IsCurrentTenantMemberAsync(
        SekaiPlatformDbContext dbContext,
        CurrentRequestContext requestContext,
        CancellationToken cancellationToken)
    {
        if (requestContext.UserId is null || requestContext.TenantId is null)
        {
            return false;
        }

        return await dbContext.UserTenants.AnyAsync(item =>
            item.TenantId == requestContext.TenantId.Value
            && item.UserId == requestContext.UserId.Value
            && item.Status == UserTenantStatuses.Active,
            cancellationToken);
    }

    /// <summary>
    /// Reads a positive integer query parameter, using a default when absent.
    /// </summary>
    private static bool TryReadPositiveInt(
        HttpContext httpContext,
        string name,
        int defaultValue,
        out int value)
    {
        var raw = httpContext.Request.Query[name].ToString();
        if (string.IsNullOrWhiteSpace(raw))
        {
            value = defaultValue;
            return true;
        }

        return int.TryParse(raw, out value) && value > 0;
    }

    /// <summary>
    /// Creates a trace-aware error response for search query endpoints.
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
