using Microsoft.EntityFrameworkCore;
using SekaiPlatform.Database;
using SekaiPlatform.Shared.Web.Auth;
using SekaiPlatform.Shared.Web.Context;
using SekaiPlatform.SourceSync;

/// <summary>
/// Maps Asset Service endpoints that read shared stories and tenant translations.
/// </summary>
internal static class AssetsEndpoints
{
    private const int DefaultPage = 1;
    private const int DefaultPageSize = 20;
    private const int MaxPageSize = 100;
    private const int MaxResultWindow = 10_000;

    private static readonly StoryTypeInfoResponse[] StoryTypes =
    [
        new(SourceSyncConstants.EventStory, "活动剧情"),
        new(SourceSyncConstants.CardStory, "卡面剧情"),
        new(SourceSyncConstants.MainStory, "主线剧情"),
        new(SourceSyncConstants.AreaTalk, "区域对话"),
        new(SourceSyncConstants.SpecialStory, "特殊剧情")
    ];

    private static readonly HashSet<string> StoryTypeValues = StoryTypes
        .Select(item => item.Value)
        .ToHashSet(StringComparer.Ordinal);

    /// <summary>
    /// Registers internal asset read endpoints.
    /// </summary>
    public static IEndpointRouteBuilder MapAssetsEndpoints(this IEndpointRouteBuilder app)
    {
        SecureAssetsRead(app.MapGet("/internal/story-types", () =>
        {
            return Results.Json(StoryTypes);
        }));

        SecureAssetsRead(app.MapGet("/internal/story-groups", async Task<IResult> (
            HttpContext httpContext,
            SekaiPlatformDbContext dbContext,
            ICurrentRequestContextAccessor contextAccessor,
            CancellationToken cancellationToken) =>
        {
            if (!TryNormalizeStoryType(httpContext.Request.Query["story_type"].ToString(), out var normalizedStoryType))
            {
                return AssetsEndpointResults.Error(contextAccessor, StatusCodes.Status400BadRequest, "剧情类型无效。");
            }

            if (!TryReadPagination(httpContext, out var paging))
            {
                return AssetsEndpointResults.Error(contextAccessor, StatusCodes.Status400BadRequest, "分页参数无效。");
            }

            var query = dbContext.StoryGroups
                .AsNoTracking()
                .Where(group => group.DeletedAt == null);
            if (normalizedStoryType is not null)
            {
                query = query.Where(group => group.StoryType == normalizedStoryType);
            }

            var normalizedKeyword = NormalizeOptionalText(httpContext.Request.Query["keyword"].ToString());
            if (normalizedKeyword is not null)
            {
                query = query.Where(group =>
                    group.Title.Contains(normalizedKeyword)
                    || (group.Subtitle != null && group.Subtitle.Contains(normalizedKeyword))
                    || (group.ExternalId != null && group.ExternalId.Contains(normalizedKeyword)));
            }

            var total = await query.CountAsync(cancellationToken);
            var offset = GetOffset(paging);
            var items = await query
                .OrderBy(group => group.StoryType)
                .ThenBy(group => group.DisplayNo == null)
                .ThenBy(group => group.DisplayNo)
                .ThenBy(group => group.Id)
                .Skip(offset)
                .Take(paging.PageSize)
                .ToArrayAsync(cancellationToken);

            return Results.Json(new StoryGroupPageResponse(
                items.Select(AssetsEndpointResults.ToResponse).ToArray(),
                new PageMetaResponse(paging.Page, paging.PageSize, total)));
        }));

        SecureAssetsRead(app.MapGet("/internal/story-groups/{storyGroupId:long}", async Task<IResult> (
            long storyGroupId,
            SekaiPlatformDbContext dbContext,
            ICurrentRequestContextAccessor contextAccessor,
            CancellationToken cancellationToken) =>
        {
            var group = await dbContext.StoryGroups
                .AsNoTracking()
                .SingleOrDefaultAsync(item => item.Id == storyGroupId && item.DeletedAt == null, cancellationToken);
            return group is null
                ? AssetsEndpointResults.Error(contextAccessor, StatusCodes.Status404NotFound, "剧情集不存在。")
                : Results.Json(AssetsEndpointResults.ToResponse(group));
        }));

        SecureAssetsRead(app.MapGet("/internal/stories", async Task<IResult> (
            HttpContext httpContext,
            SekaiPlatformDbContext dbContext,
            ICurrentRequestContextAccessor contextAccessor,
            CancellationToken cancellationToken) =>
        {
            if (!TryNormalizeStoryType(httpContext.Request.Query["story_type"].ToString(), out var normalizedStoryType))
            {
                return AssetsEndpointResults.Error(contextAccessor, StatusCodes.Status400BadRequest, "剧情类型无效。");
            }

            if (!TryReadOptionalLong(httpContext, "story_group_id", out var storyGroupId))
            {
                return AssetsEndpointResults.Error(contextAccessor, StatusCodes.Status400BadRequest, "剧情集 ID 无效。");
            }

            if (!TryReadPagination(httpContext, out var paging))
            {
                return AssetsEndpointResults.Error(contextAccessor, StatusCodes.Status400BadRequest, "分页参数无效。");
            }

            var query = dbContext.Stories
                .AsNoTracking()
                .Include(story => story.Group)
                .Where(story => story.DeletedAt == null && (story.Group == null || story.Group.DeletedAt == null));
            if (storyGroupId is not null)
            {
                query = query.Where(story => story.GroupId == storyGroupId.Value);
            }

            if (normalizedStoryType is not null)
            {
                query = query.Where(story => story.StoryType == normalizedStoryType);
            }

            var normalizedKeyword = NormalizeOptionalText(httpContext.Request.Query["keyword"].ToString());
            if (normalizedKeyword is not null)
            {
                query = query.Where(story =>
                    story.Title.Contains(normalizedKeyword)
                    || story.ScenarioId.Contains(normalizedKeyword));
            }

            var total = await query.CountAsync(cancellationToken);
            var offset = GetOffset(paging);
            var items = await query
                .OrderBy(story => story.GroupId)
                .ThenBy(story => story.SortOrder)
                .ThenBy(story => story.Id)
                .Skip(offset)
                .Take(paging.PageSize)
                .ToArrayAsync(cancellationToken);

            return Results.Json(new StoryPageResponse(
                items.Select(AssetsEndpointResults.ToResponse).ToArray(),
                new PageMetaResponse(paging.Page, paging.PageSize, total)));
        }));

        SecureAssetsRead(app.MapGet("/internal/stories/{storyId:long}", async Task<IResult> (
            long storyId,
            SekaiPlatformDbContext dbContext,
            ICurrentRequestContextAccessor contextAccessor,
            CancellationToken cancellationToken) =>
        {
            var story = await dbContext.Stories
                .AsNoTracking()
                .Include(item => item.Group)
                .SingleOrDefaultAsync(item =>
                    item.Id == storyId
                    && item.DeletedAt == null
                    && (item.Group == null || item.Group.DeletedAt == null),
                    cancellationToken);
            return story is null
                ? AssetsEndpointResults.Error(contextAccessor, StatusCodes.Status404NotFound, "剧情不存在。")
                : Results.Json(AssetsEndpointResults.ToResponse(story));
        }));

        SecureAssetsRead(app.MapGet("/internal/stories/{storyId:long}/source-lines", async Task<IResult> (
            long storyId,
            SekaiPlatformDbContext dbContext,
            ICurrentRequestContextAccessor contextAccessor,
            CancellationToken cancellationToken) =>
        {
            if (!await StoryExistsAsync(dbContext, storyId, cancellationToken))
            {
                return AssetsEndpointResults.Error(contextAccessor, StatusCodes.Status404NotFound, "剧情不存在。");
            }

            var lines = await dbContext.StorySourceLines
                .AsNoTracking()
                .Where(line => line.StoryId == storyId)
                .OrderBy(line => line.LineNo)
                .ThenBy(line => line.Id)
                .ToArrayAsync(cancellationToken);
            return Results.Json(lines.Select(AssetsEndpointResults.ToResponse).ToArray());
        }));

        SecureAssetsRead(app.MapGet("/internal/stories/{storyId:long}/translation-versions", async Task<IResult> (
            long storyId,
            HttpContext httpContext,
            SekaiPlatformDbContext dbContext,
            ICurrentRequestContextAccessor contextAccessor,
            CancellationToken cancellationToken) =>
        {
            if (!TryReadPagination(httpContext, out var paging))
            {
                return AssetsEndpointResults.Error(contextAccessor, StatusCodes.Status400BadRequest, "分页参数无效。");
            }

            if (!await StoryExistsAsync(dbContext, storyId, cancellationToken))
            {
                return AssetsEndpointResults.Error(contextAccessor, StatusCodes.Status404NotFound, "剧情不存在。");
            }

            var tenantId = contextAccessor.GetCurrent().TenantId!.Value;
            var query = dbContext.TranslationVersions
                .AsNoTracking()
                .Where(version =>
                    version.TenantId == tenantId
                    && version.StoryId == storyId
                    && version.DeletedAt == null);
            var total = await query.CountAsync(cancellationToken);
            var offset = GetOffset(paging);
            var items = await query
                .OrderByDescending(version => version.VersionNo)
                .ThenByDescending(version => version.Id)
                .Skip(offset)
                .Take(paging.PageSize)
                .ToArrayAsync(cancellationToken);

            return Results.Json(new TranslationVersionPageResponse(
                items.Select(AssetsEndpointResults.ToResponse).ToArray(),
                new PageMetaResponse(paging.Page, paging.PageSize, total)));
        }));

        SecureAssetsRead(app.MapGet("/internal/translation-versions/{translationVersionId:long}", async Task<IResult> (
            long translationVersionId,
            SekaiPlatformDbContext dbContext,
            ICurrentRequestContextAccessor contextAccessor,
            CancellationToken cancellationToken) =>
        {
            var version = await GetCurrentTenantVersionAsync(
                dbContext,
                contextAccessor.GetCurrent().TenantId!.Value,
                translationVersionId,
                cancellationToken);
            return version is null
                ? AssetsEndpointResults.Error(contextAccessor, StatusCodes.Status404NotFound, "译文版本不存在。")
                : Results.Json(AssetsEndpointResults.ToResponse(version));
        }));

        SecureAssetsRead(app.MapGet("/internal/translation-versions/{translationVersionId:long}/lines", async Task<IResult> (
            long translationVersionId,
            SekaiPlatformDbContext dbContext,
            ICurrentRequestContextAccessor contextAccessor,
            CancellationToken cancellationToken) =>
        {
            var version = await GetCurrentTenantVersionAsync(
                dbContext,
                contextAccessor.GetCurrent().TenantId!.Value,
                translationVersionId,
                cancellationToken);
            if (version is null)
            {
                return AssetsEndpointResults.Error(contextAccessor, StatusCodes.Status404NotFound, "译文版本不存在。");
            }

            var lines = await dbContext.TranslationLines
                .AsNoTracking()
                .Where(line => line.VersionId == translationVersionId)
                .OrderBy(line => line.LineNo)
                .ThenBy(line => line.Id)
                .ToArrayAsync(cancellationToken);
            return Results.Json(lines.Select(AssetsEndpointResults.ToResponse).ToArray());
        }));

        return app;
    }

    /// <summary>
    /// Applies the shared internal authorization and tenant membership guard for asset read endpoints.
    /// </summary>
    private static RouteHandlerBuilder SecureAssetsRead(RouteHandlerBuilder builder)
    {
        return builder
            .RequireInternalAuthorization(
                SekaiInternalAuthDefaults.AssetsReadScope,
                [SekaiInternalAuthDefaults.ApiServiceActor],
                requireSubject: true,
                requireTenant: true)
            .AddEndpointFilter(async (context, next) =>
            {
                var dbContext = context.HttpContext.RequestServices.GetRequiredService<SekaiPlatformDbContext>();
                var contextAccessor = context.HttpContext.RequestServices.GetRequiredService<ICurrentRequestContextAccessor>();
                if (!await TenantMemberGuard.IsCurrentTenantMemberAsync(
                    dbContext,
                    contextAccessor,
                    context.HttpContext.RequestAborted))
                {
                    return AssetsEndpointResults.Error(contextAccessor, StatusCodes.Status403Forbidden, "无权访问。");
                }

                return await next(context);
            });
    }

    /// <summary>
    /// Verifies whether a story type query value is absent or supported.
    /// </summary>
    private static bool TryNormalizeStoryType(string? storyType, out string? normalized)
    {
        normalized = NormalizeOptionalText(storyType);
        return normalized is null || StoryTypeValues.Contains(normalized);
    }

    /// <summary>
    /// Reads standard page and page size query parameters.
    /// </summary>
    private static bool TryReadPagination(HttpContext httpContext, out Pagination pagination)
    {
        if (!TryReadPositiveInt(httpContext, "page", DefaultPage, out var normalizedPage)
            || !TryReadPositiveInt(httpContext, "page_size", DefaultPageSize, out var normalizedPageSize))
        {
            pagination = new Pagination(DefaultPage, DefaultPageSize);
            return false;
        }

        pagination = new Pagination(normalizedPage, normalizedPageSize);
        return normalizedPageSize <= MaxPageSize
            && ((long)normalizedPage - 1) * normalizedPageSize + normalizedPageSize <= MaxResultWindow;
    }

    /// <summary>
    /// Reads an optional positive long query parameter.
    /// </summary>
    private static bool TryReadOptionalLong(HttpContext httpContext, string name, out long? value)
    {
        var raw = httpContext.Request.Query[name].ToString();
        if (string.IsNullOrWhiteSpace(raw))
        {
            value = null;
            return true;
        }

        var success = long.TryParse(raw, out var parsed) && parsed > 0;
        value = parsed;
        return success;
    }

    /// <summary>
    /// Reads a positive integer query parameter, using a default when absent.
    /// </summary>
    private static bool TryReadPositiveInt(HttpContext httpContext, string name, int defaultValue, out int value)
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
    /// Converts validated paging parameters into an EF Core skip offset.
    /// </summary>
    private static int GetOffset(Pagination pagination)
    {
        return (pagination.Page - 1) * pagination.PageSize;
    }

    /// <summary>
    /// Checks whether a shared story exists and is not soft-deleted.
    /// </summary>
    private static async Task<bool> StoryExistsAsync(
        SekaiPlatformDbContext dbContext,
        long storyId,
        CancellationToken cancellationToken)
    {
        return await dbContext.Stories.AnyAsync(story =>
            story.Id == storyId
            && story.DeletedAt == null
            && (story.Group == null || story.Group.DeletedAt == null),
            cancellationToken);
    }

    /// <summary>
    /// Loads a translation version that belongs to the current tenant and an active story.
    /// </summary>
    private static async Task<TranslationVersion?> GetCurrentTenantVersionAsync(
        SekaiPlatformDbContext dbContext,
        long tenantId,
        long translationVersionId,
        CancellationToken cancellationToken)
    {
        return await dbContext.TranslationVersions
            .AsNoTracking()
            .Include(version => version.Story)
            .ThenInclude(story => story!.Group)
            .SingleOrDefaultAsync(version =>
                version.Id == translationVersionId
                && version.TenantId == tenantId
                && version.DeletedAt == null
                && version.Story != null
                && version.Story.DeletedAt == null
                && (version.Story.Group == null || version.Story.Group.DeletedAt == null),
                cancellationToken);
    }

    /// <summary>
    /// Normalizes optional query text before filtering.
    /// </summary>
    private static string? NormalizeOptionalText(string? value)
    {
        var trimmed = value?.Trim();
        return string.IsNullOrWhiteSpace(trimmed) ? null : trimmed;
    }

    private sealed record Pagination(int Page, int PageSize);
}
