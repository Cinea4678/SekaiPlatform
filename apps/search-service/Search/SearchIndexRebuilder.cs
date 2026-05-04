using Microsoft.EntityFrameworkCore;
using SekaiPlatform.Database;

namespace SekaiPlatform.SearchService.Search;

/// <summary>
/// Rebuilds Elasticsearch language asset documents from the PostgreSQL source of truth.
/// </summary>
internal sealed class SearchIndexRebuilder(
    SekaiPlatformDbContext dbContext,
    ElasticsearchIndexClient elasticsearch)
{
    /// <summary>
    /// Rebuilds index documents for the requested scope and filters.
    /// </summary>
    public async Task<SearchIndexRebuildResponse> RebuildAsync(
        SearchIndexRebuildRequest request,
        CancellationToken cancellationToken)
    {
        var scope = NormalizeScope(request.Scope);
        await elasticsearch.EnsureIndexAsync(request.ForceRecreate, cancellationToken);
        if (!request.ForceRecreate)
        {
            await elasticsearch.DeleteDocumentsAsync(request, scope, cancellationToken);
        }

        var sourceIndexed = 0;
        var translationIndexed = 0;
        if (scope is SearchIndexConstants.ScopeAll or SearchIndexConstants.ScopeSource)
        {
            var sourceDocuments = await BuildSourceDocumentsAsync(request, cancellationToken);
            sourceIndexed = await elasticsearch.BulkIndexAsync(sourceDocuments, cancellationToken);
        }

        if (scope is SearchIndexConstants.ScopeAll or SearchIndexConstants.ScopeTranslation)
        {
            var translationDocuments = await BuildTranslationDocumentsAsync(request, cancellationToken);
            translationIndexed = await elasticsearch.BulkIndexAsync(translationDocuments, cancellationToken);
        }

        return new SearchIndexRebuildResponse(
            scope,
            Deleted: !request.ForceRecreate,
            sourceIndexed,
            translationIndexed);
    }

    /// <summary>
    /// Converts the optional request scope into a supported canonical scope.
    /// </summary>
    public static string NormalizeScope(string? scope)
    {
        return string.IsNullOrWhiteSpace(scope)
            ? SearchIndexConstants.ScopeAll
            : scope.Trim().ToLowerInvariant();
    }

    /// <summary>
    /// Builds source line index documents from active stories and groups.
    /// </summary>
    private async Task<IReadOnlyList<SearchIndexDocument>> BuildSourceDocumentsAsync(
        SearchIndexRebuildRequest request,
        CancellationToken cancellationToken)
    {
        var query =
            from line in dbContext.StorySourceLines.AsNoTracking()
            join story in dbContext.Stories.AsNoTracking() on line.StoryId equals story.Id
            join storyGroup in dbContext.StoryGroups.AsNoTracking() on story.GroupId equals storyGroup.Id into storyGroups
            from storyGroup in storyGroups.DefaultIfEmpty()
            where story.DeletedAt == null && (storyGroup == null || storyGroup.DeletedAt == null)
            select new { line, story, storyGroup };

        if (request.StoryIds is { Length: > 0 })
        {
            query = query.Where(item => request.StoryIds.Contains(item.story.Id));
        }

        return await query
            .OrderBy(item => item.story.Id)
            .ThenBy(item => item.line.LineNo)
            .Select(item => new SearchIndexDocument
            {
                DocumentId = $"source:{item.line.Id}",
                AssetType = SearchIndexConstants.AssetTypeSource,
                TenantId = null,
                StoryId = item.story.Id,
                StoryType = item.story.StoryType,
                ScenarioId = item.story.ScenarioId,
                StoryTitle = item.story.Title,
                StoryGroupId = item.storyGroup == null ? null : item.storyGroup.Id,
                StoryGroupTitle = item.storyGroup == null ? null : item.storyGroup.Title,
                TranslationVersionId = null,
                SourceLineId = item.line.Id,
                LineNo = item.line.LineNo,
                Speaker = item.line.Speaker,
                Text = item.line.Text
            })
            .ToArrayAsync(cancellationToken);
    }

    /// <summary>
    /// Builds tenant translation line index documents from active translation versions.
    /// </summary>
    private async Task<IReadOnlyList<SearchIndexDocument>> BuildTranslationDocumentsAsync(
        SearchIndexRebuildRequest request,
        CancellationToken cancellationToken)
    {
        var query =
            from line in dbContext.TranslationLines.AsNoTracking()
            join version in dbContext.TranslationVersions.AsNoTracking() on
                new { line.VersionId, line.StoryId } equals new { VersionId = version.Id, version.StoryId }
            join story in dbContext.Stories.AsNoTracking() on line.StoryId equals story.Id
            join storyGroup in dbContext.StoryGroups.AsNoTracking() on story.GroupId equals storyGroup.Id into storyGroups
            from storyGroup in storyGroups.DefaultIfEmpty()
            where version.DeletedAt == null
                && story.DeletedAt == null
                && (storyGroup == null || storyGroup.DeletedAt == null)
            select new { line, version, story, storyGroup };

        if (request.StoryIds is { Length: > 0 })
        {
            query = query.Where(item => request.StoryIds.Contains(item.story.Id));
        }

        if (request.TenantId is not null)
        {
            query = query.Where(item => item.version.TenantId == request.TenantId.Value);
        }

        if (request.TranslationVersionId is not null)
        {
            query = query.Where(item => item.version.Id == request.TranslationVersionId.Value);
        }

        return await query
            .OrderBy(item => item.version.Id)
            .ThenBy(item => item.line.LineNo)
            .Select(item => new SearchIndexDocument
            {
                DocumentId = $"translation:{item.line.Id}",
                AssetType = SearchIndexConstants.AssetTypeTranslation,
                TenantId = item.version.TenantId,
                StoryId = item.story.Id,
                StoryType = item.story.StoryType,
                ScenarioId = item.story.ScenarioId,
                StoryTitle = item.story.Title,
                StoryGroupId = item.storyGroup == null ? null : item.storyGroup.Id,
                StoryGroupTitle = item.storyGroup == null ? null : item.storyGroup.Title,
                TranslationVersionId = item.version.Id,
                SourceLineId = item.line.SourceLineId,
                LineNo = item.line.LineNo,
                Speaker = item.line.Speaker,
                Text = item.line.Text
            })
            .ToArrayAsync(cancellationToken);
    }
}
