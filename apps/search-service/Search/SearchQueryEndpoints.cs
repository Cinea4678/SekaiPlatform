using System.Text.Json;
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
            var enrichedResponse = await EnrichSearchResponseAsync(
                dbContext,
                response,
                requestContext.TenantId.Value,
                cancellationToken);
            return Results.Json(enrichedResponse);
        }).RequireInternalAuthorization(
            SekaiInternalAuthDefaults.SearchQueryScope,
            [SekaiInternalAuthDefaults.ApiServiceActor],
            requireSubject: true,
            requireTenant: true);

        return app;
    }

    /// <summary>
    /// Adds source and current-tenant translation context for each line-level search hit.
    /// </summary>
    private static async Task<SearchQueryResponse> EnrichSearchResponseAsync(
        SekaiPlatformDbContext dbContext,
        SearchQueryResponse response,
        long tenantId,
        CancellationToken cancellationToken)
    {
        if (response.Items.Count == 0)
        {
            return response;
        }

        var sourceLineIds = response.Items
            .Select(item => item.SourceLineId)
            .Distinct()
            .ToArray();

        var sources = await dbContext.StorySourceLines
            .AsNoTracking()
            .Where(line => sourceLineIds.Contains(line.Id))
            .Select(line => new SearchSourceLineReference
            {
                SourceLineId = line.Id,
                Text = line.Text,
                Speaker = line.Speaker
            })
            .ToDictionaryAsync(line => line.SourceLineId, cancellationToken);

        var translationRows = await (
                from line in dbContext.TranslationLines.AsNoTracking()
                join version in dbContext.TranslationVersions.AsNoTracking() on
                    new { line.VersionId, line.StoryId } equals new { VersionId = version.Id, version.StoryId }
                where sourceLineIds.Contains(line.SourceLineId)
                    && version.TenantId == tenantId
                    && version.DeletedAt == null
                orderby line.SourceLineId, version.VersionNo, version.Id, line.Id
                select new
                {
                    line.SourceLineId,
                    TranslationLineId = line.Id,
                    TranslationVersionId = version.Id,
                    version.VersionNo,
                    TranslationVersionTitle = version.Title,
                    version.Metadata,
                    line.Text,
                    line.Speaker
                })
            .ToArrayAsync(cancellationToken);

        var translationsBySourceLine = translationRows
            .GroupBy(item => item.SourceLineId)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyList<SearchTranslationLineReference>)group
                    .Select(item => new SearchTranslationLineReference
                    {
                        TranslationLineId = item.TranslationLineId,
                        TranslationVersionId = item.TranslationVersionId,
                        VersionNo = item.VersionNo,
                        TranslationVersionTitle = item.TranslationVersionTitle,
                        Staff = ParseTranslationStaff(item.Metadata),
                        Text = item.Text,
                        Speaker = item.Speaker
                    })
                    .ToArray());

        var items = response.Items
            .Select(item => item with
            {
                Staff = FindTranslationStaff(translationsBySourceLine, item),
                Source = sources.GetValueOrDefault(item.SourceLineId),
                Translations = translationsBySourceLine.GetValueOrDefault(item.SourceLineId)
                    ?? []
            })
            .ToArray();

        return response with { Items = items };
    }

    /// <summary>
    /// Finds staff metadata for the translated line that produced the search hit.
    /// </summary>
    private static SearchTranslationStaffReference? FindTranslationStaff(
        IReadOnlyDictionary<long, IReadOnlyList<SearchTranslationLineReference>> translationsBySourceLine,
        SearchQueryHit item)
    {
        if (item.TranslationLineId is null
            || !translationsBySourceLine.TryGetValue(item.SourceLineId, out var translations))
        {
            return null;
        }

        return translations
            .FirstOrDefault(translation => translation.TranslationLineId == item.TranslationLineId.Value)
            ?.Staff;
    }

    /// <summary>
    /// Extracts the fixed staff attribution object from translation version metadata.
    /// </summary>
    private static SearchTranslationStaffReference? ParseTranslationStaff(string? metadata)
    {
        if (string.IsNullOrWhiteSpace(metadata))
        {
            return null;
        }

        using var document = JsonDocument.Parse(metadata);
        if (!document.RootElement.TryGetProperty("staff", out var staff)
            || staff.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
        {
            return null;
        }

        if (staff.ValueKind is not JsonValueKind.Object)
        {
            return null;
        }

        return new SearchTranslationStaffReference
        {
            Translator = ReadOptionalStaffName(staff, "translator"),
            Proofreader = ReadOptionalStaffName(staff, "proofreader"),
            Approver = ReadOptionalStaffName(staff, "approver")
        };
    }

    /// <summary>
    /// Reads one optional staff display name from the metadata staff object.
    /// </summary>
    private static string? ReadOptionalStaffName(JsonElement staff, string propertyName)
    {
        return staff.TryGetProperty(propertyName, out var value)
            && value.ValueKind == JsonValueKind.String
                ? value.GetString()
                : null;
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
