using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SekaiPlatform.Database;

/// <summary>
/// Builds Open API public translation responses from published translation versions.
/// </summary>
internal static class PublicTranslationEndpointResults
{
    /// <summary>
    /// Loads one scenario result with translated lines included.
    /// </summary>
    public static async Task<PublicTranslationResult> GetScenarioAsync(
        SekaiPlatformDbContext dbContext,
        string scenarioId,
        CancellationToken cancellationToken)
    {
        var results = await GetScenariosAsync(dbContext, [scenarioId], includeLines: true, cancellationToken);
        return results.Single();
    }

    /// <summary>
    /// Loads scenario results in request order, omitting translated line contents.
    /// </summary>
    public static async Task<IReadOnlyList<PublicTranslationResult>> GetScenariosAsync(
        SekaiPlatformDbContext dbContext,
        IReadOnlyList<string> scenarioIds,
        bool includeLines,
        CancellationToken cancellationToken)
    {
        var distinctScenarioIds = scenarioIds.Distinct(StringComparer.Ordinal).ToArray();
        var stories = await dbContext.Stories
            .AsNoTracking()
            .Include(story => story.Group)
            .Where(story =>
                distinctScenarioIds.Contains(story.ScenarioId)
                && story.DeletedAt == null
                && (story.Group == null || story.Group.DeletedAt == null))
            .Select(story => new
            {
                story.Id,
                story.ScenarioId
            })
            .ToArrayAsync(cancellationToken);

        if (stories.Length == 0)
        {
            return scenarioIds
                .Select(scenarioId => new PublicTranslationResult(scenarioId, false, []))
                .ToArray();
        }

        var storyIds = stories.Select(story => story.Id).ToArray();
        var storyScenarioIds = stories.ToDictionary(story => story.Id, story => story.ScenarioId);
        var versions = await dbContext.TranslationVersions
            .AsNoTracking()
            .Include(version => version.Tenant)
            .Where(version =>
                storyIds.Contains(version.StoryId)
                && version.IsPublished
                && version.DeletedAt == null)
            .OrderByDescending(version => version.UpdatedAt)
            .ThenByDescending(version => version.Id)
            .ToArrayAsync(cancellationToken);

        if (versions.Length == 0)
        {
            return scenarioIds
                .Select(scenarioId => new PublicTranslationResult(scenarioId, false, []))
                .ToArray();
        }

        var versionIds = versions.Select(version => version.Id).ToArray();
        var lineCounts = await dbContext.TranslationLines
            .AsNoTracking()
            .Where(line => versionIds.Contains(line.VersionId))
            .GroupBy(line => line.VersionId)
            .Select(group => new
            {
                VersionId = group.Key,
                Count = group.Count()
            })
            .ToDictionaryAsync(item => item.VersionId, item => item.Count, cancellationToken);

        var linesByVersion = includeLines
            ? await LoadLinesByVersionAsync(dbContext, versionIds, cancellationToken)
            : new Dictionary<long, IReadOnlyList<PublicTranslationLine>>();

        var translationsByScenario = versions
            .GroupBy(version => storyScenarioIds[version.StoryId], StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyList<PublicTranslationInfo>)group
                    .Select(version => ToInfo(
                        version,
                        lineCounts.GetValueOrDefault(version.Id),
                        linesByVersion.GetValueOrDefault(version.Id)))
                    .ToArray(),
                StringComparer.Ordinal);

        return scenarioIds
            .Select(scenarioId =>
            {
                var translations = translationsByScenario.GetValueOrDefault(scenarioId) ?? [];
                return new PublicTranslationResult(scenarioId, translations.Count > 0, translations);
            })
            .ToArray();
    }

    private static async Task<Dictionary<long, IReadOnlyList<PublicTranslationLine>>> LoadLinesByVersionAsync(
        SekaiPlatformDbContext dbContext,
        IReadOnlyList<long> versionIds,
        CancellationToken cancellationToken)
    {
        var lines = await dbContext.TranslationLines
            .AsNoTracking()
            .Include(line => line.SourceLine)
            .Where(line => versionIds.Contains(line.VersionId))
            .OrderBy(line => line.LineNo)
            .ThenBy(line => line.Id)
            .ToArrayAsync(cancellationToken);

        return lines
            .GroupBy(line => line.VersionId)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyList<PublicTranslationLine>)group
                    .Select(line => new PublicTranslationLine(
                        line.LineNo,
                        line.SourceLine!.LineType,
                        line.Speaker,
                        line.Text))
                    .ToArray());
    }

    private static PublicTranslationInfo ToInfo(
        TranslationVersion version,
        int lineCount,
        IReadOnlyList<PublicTranslationLine>? lines)
    {
        return new PublicTranslationInfo(
            version.Id,
            version.VersionNo,
            version.Title,
            new PublicTenant(
                version.Tenant!.Id,
                version.Tenant.Name,
                version.Tenant.AvatarUrl),
            ReadStaff(version.Metadata),
            lineCount,
            version.CreatedAt,
            version.UpdatedAt,
            lines);
    }

    private static PublicTranslationStaff ReadStaff(string? metadata)
    {
        if (string.IsNullOrWhiteSpace(metadata))
        {
            return new PublicTranslationStaff(null, null, null);
        }

        try
        {
            using var document = JsonDocument.Parse(metadata);
            if (!document.RootElement.TryGetProperty("staff", out var staff)
                || staff.ValueKind != JsonValueKind.Object)
            {
                return new PublicTranslationStaff(null, null, null);
            }

            return new PublicTranslationStaff(
                ReadOptionalString(staff, "translator"),
                ReadOptionalString(staff, "proofreader"),
                ReadOptionalString(staff, "approver"));
        }
        catch (JsonException)
        {
            return new PublicTranslationStaff(null, null, null);
        }
    }

    private static string? ReadOptionalString(JsonElement element, string propertyName)
    {
        return element.TryGetProperty(propertyName, out var property)
            && property.ValueKind == JsonValueKind.String
            ? property.GetString()
            : null;
    }
}
