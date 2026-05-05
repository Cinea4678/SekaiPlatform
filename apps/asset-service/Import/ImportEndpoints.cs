using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SekaiPlatform.Database;
using SekaiPlatform.Shared.Web.Auth;
using SekaiPlatform.Shared.Web.Context;
using SekaiPlatform.Shared.Web.Responses;
using SekaiPlatform.Shared.Web.Search;

/// <summary>
/// Maps Asset Service endpoints that import tenant-owned translation versions.
/// </summary>
internal static class ImportEndpoints
{
    private const int MaxItemsPerRequest = 100;
    private const int MaxTotalLinesPerRequest = 10000;
    private const int MaxLinesPerItem = 5000;
    private const int MaxTitleLength = 255;
    private const int MaxSpeakerLength = 128;
    private const int MaxTextLength = 20000;
    private const int MaxMetadataLength = 20000;

    /// <summary>
    /// Registers internal translation import endpoints.
    /// </summary>
    public static IEndpointRouteBuilder MapImportEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/internal/import/translation-versions", async Task<IResult> (
            HttpContext httpContext,
            SekaiPlatformDbContext dbContext,
            ICurrentRequestContextAccessor contextAccessor,
            SearchIndexRefreshClient searchIndexRefreshClient,
            ILoggerFactory loggerFactory,
            CancellationToken cancellationToken) =>
        {
            if (!await TenantAdminGuard.IsCurrentTenantAdminAsync(dbContext, contextAccessor, cancellationToken))
            {
                return Error(contextAccessor, StatusCodes.Status403Forbidden, "Forbidden.");
            }

            var readResult = await ReadRequestAsync(httpContext, cancellationToken);
            if (!readResult.Success)
            {
                return Error(contextAccessor, StatusCodes.Status400BadRequest, "Invalid translation import request.");
            }

            var validation = ValidateRequest(readResult.Request);
            if (!validation.Success)
            {
                return Error(contextAccessor, StatusCodes.Status400BadRequest, validation.Error!);
            }

            var context = contextAccessor.GetCurrent();
            var tenantId = context.TenantId!.Value;
            var userId = context.UserId!.Value;
            var resolved = await ResolveStoriesAndLinesAsync(
                dbContext,
                validation.Items,
                cancellationToken);
            if (!resolved.Success)
            {
                return Error(contextAccessor, resolved.StatusCode, resolved.Error!);
            }

            var result = await PersistImportAsync(
                dbContext,
                tenantId,
                userId,
                resolved.Items,
                cancellationToken);

            await RefreshImportedVersionsAsync(
                result.Items,
                tenantId,
                searchIndexRefreshClient,
                loggerFactory.CreateLogger("SekaiPlatform.Import"),
                CancellationToken.None);

            return Results.Json(new TranslationImportResponse(
                result.Items
                    .Select(item => new TranslationImportVersionResponse(
                        item.StoryType,
                        item.ScenarioId,
                        item.StoryId,
                        item.TranslationVersionId,
                        item.VersionNo,
                        item.LineCount))
                    .ToArray(),
                result.Items.Count,
                result.Items.Sum(item => item.LineCount)));
        }).RequireInternalAuthorization(
            SekaiInternalAuthDefaults.TranslationsImportWriteScope,
            [SekaiInternalAuthDefaults.ApiServiceActor],
            requireSubject: true,
            requireTenant: true);

        return app;
    }

    /// <summary>
    /// Reads a JSON import request without letting malformed JSON escape the endpoint.
    /// </summary>
    private static async Task<RequestReadResult> ReadRequestAsync(
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        if (!IsJsonContent(httpContext.Request.ContentType))
        {
            return new RequestReadResult(false, null);
        }

        try
        {
            var request = await JsonSerializer.DeserializeAsync<TranslationImportRequest>(
                httpContext.Request.Body,
                cancellationToken: cancellationToken);
            return new RequestReadResult(true, request);
        }
        catch (JsonException)
        {
            return new RequestReadResult(false, null);
        }
    }

    /// <summary>
    /// Validates the request shape before database writes begin.
    /// </summary>
    private static ValidationResult ValidateRequest(TranslationImportRequest? request)
    {
        if (request?.Items is null || request.Items.Length == 0)
        {
            return ValidationResult.Failed("Import items are required.");
        }

        if (request.Items.Length > MaxItemsPerRequest)
        {
            return ValidationResult.Failed("Too many import items.");
        }

        var totalLines = 0;
        var items = new List<ValidatedImportItem>(request.Items.Length);
        foreach (var item in request.Items)
        {
            var storyType = item.StoryType?.Trim();
            var scenarioId = item.ScenarioId?.Trim();
            if (string.IsNullOrWhiteSpace(storyType) || string.IsNullOrWhiteSpace(scenarioId))
            {
                return ValidationResult.Failed("Story type and scenario ID are required.");
            }

            if (item.Lines is null || item.Lines.Length == 0)
            {
                return ValidationResult.Failed("Import lines are required.");
            }

            if (item.Lines.Length > MaxLinesPerItem)
            {
                return ValidationResult.Failed("Too many import lines in one item.");
            }

            totalLines += item.Lines.Length;
            if (totalLines > MaxTotalLinesPerRequest)
            {
                return ValidationResult.Failed("Too many import lines.");
            }

            var title = NormalizeOptionalText(item.Title);
            if (title?.Length > MaxTitleLength)
            {
                return ValidationResult.Failed("Translation version title is too long.");
            }

            if (!IsValidVersionMetadata(item.Metadata))
            {
                return ValidationResult.Failed("Translation version metadata is invalid.");
            }

            var itemMetadata = SerializeMetadata(item.Metadata);
            if (itemMetadata?.Length > MaxMetadataLength)
            {
                return ValidationResult.Failed("Translation version metadata is too large.");
            }

            var duplicateLine = item.Lines
                .GroupBy(line => line.LineNo)
                .FirstOrDefault(group => group.Count() > 1);
            if (duplicateLine is not null)
            {
                return ValidationResult.Failed($"Duplicate line number: {duplicateLine.Key}.");
            }

            var lines = new List<ValidatedImportLine>(item.Lines.Length);
            foreach (var line in item.Lines)
            {
                var text = line.Text?.Trim();
                if (line.LineNo <= 0 || string.IsNullOrWhiteSpace(text))
                {
                    return ValidationResult.Failed("Line number and text are required.");
                }

                if (text.Length > MaxTextLength)
                {
                    return ValidationResult.Failed("Translation text is too long.");
                }

                var speaker = NormalizeOptionalText(line.Speaker);
                if (speaker?.Length > MaxSpeakerLength)
                {
                    return ValidationResult.Failed("Speaker is too long.");
                }

                if (!IsValidMetadata(line.Metadata))
                {
                    return ValidationResult.Failed("Metadata must be a JSON object.");
                }

                var metadata = SerializeMetadata(line.Metadata);
                if (metadata?.Length > MaxMetadataLength)
                {
                    return ValidationResult.Failed("Metadata is too large.");
                }

                lines.Add(new ValidatedImportLine(
                    line.LineNo,
                    text,
                    speaker,
                    metadata));
            }

            items.Add(new ValidatedImportItem(
                storyType,
                scenarioId,
                title,
                itemMetadata,
                lines));
        }

        return ValidationResult.Succeeded(items);
    }

    /// <summary>
    /// Resolves requested story keys and line numbers to existing source records.
    /// </summary>
    private static async Task<ResolvedResult> ResolveStoriesAndLinesAsync(
        SekaiPlatformDbContext dbContext,
        IReadOnlyList<ValidatedImportItem> imports,
        CancellationToken cancellationToken)
    {
        var storyTypes = imports.Select(item => item.StoryType).Distinct().ToArray();
        var scenarioIds = imports.Select(item => item.ScenarioId).Distinct().ToArray();
        var stories = await dbContext.Stories
            .AsNoTracking()
            .Where(story =>
                story.DeletedAt == null
                && storyTypes.Contains(story.StoryType)
                && scenarioIds.Contains(story.ScenarioId))
            .ToArrayAsync(cancellationToken);
        var storyByKey = stories.ToDictionary(item => (item.StoryType, item.ScenarioId));

        var storyIds = stories.Select(story => story.Id).ToArray();
        var sourceLines = await dbContext.StorySourceLines
            .AsNoTracking()
            .Where(line => storyIds.Contains(line.StoryId))
            .ToArrayAsync(cancellationToken);
        var sourceLinesByStory = sourceLines
            .GroupBy(line => line.StoryId)
            .ToDictionary(
                group => group.Key,
                group => group.ToDictionary(line => line.LineNo));

        var resolved = new List<ResolvedImportItem>(imports.Count);
        foreach (var import in imports)
        {
            if (!storyByKey.TryGetValue((import.StoryType, import.ScenarioId), out var story))
            {
                return ResolvedResult.Failed(StatusCodes.Status404NotFound, "Story not found.");
            }

            if (!sourceLinesByStory.TryGetValue(story.Id, out var linesByNo))
            {
                return ResolvedResult.Failed(StatusCodes.Status400BadRequest, "Story has no source lines.");
            }

            var requestedLines = new List<ResolvedImportLine>(import.Lines.Count);
            foreach (var line in import.Lines)
            {
                if (!linesByNo.TryGetValue(line.LineNo, out var sourceLine))
                {
                    return ResolvedResult.Failed(StatusCodes.Status400BadRequest, "Source line not found.");
                }

                requestedLines.Add(new ResolvedImportLine(
                    sourceLine.Id,
                    line.LineNo,
                    line.Text,
                    line.Speaker ?? NormalizeOptionalText(sourceLine.Speaker),
                    line.Metadata));
            }

            resolved.Add(new ResolvedImportItem(
                import.StoryType,
                import.ScenarioId,
                import.Title,
                import.Metadata,
                story.Id,
                requestedLines));
        }

        return ResolvedResult.Succeeded(resolved);
    }

    /// <summary>
    /// Persists all validated imports in one database transaction.
    /// </summary>
    private static async Task<PersistImportResult> PersistImportAsync(
        SekaiPlatformDbContext dbContext,
        long tenantId,
        long userId,
        IReadOnlyList<ResolvedImportItem> imports,
        CancellationToken cancellationToken)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        var storyIds = imports.Select(item => item.StoryId).Distinct().ToArray();
        foreach (var storyId in storyIds.Order())
        {
            await AcquireVersionLockAsync(dbContext, tenantId, storyId, cancellationToken);
        }

        var existingVersions = await dbContext.TranslationVersions
            .Where(version => version.TenantId == tenantId && storyIds.Contains(version.StoryId))
            .GroupBy(version => version.StoryId)
            .Select(group => new { StoryId = group.Key, VersionNo = group.Max(version => version.VersionNo) })
            .ToArrayAsync(cancellationToken);
        var nextVersionByStory = existingVersions.ToDictionary(item => item.StoryId, item => item.VersionNo);
        var now = DateTimeOffset.UtcNow;
        var created = new List<CreatedImportItem>(imports.Count);

        foreach (var import in imports)
        {
            nextVersionByStory.TryGetValue(import.StoryId, out var currentMaxVersion);
            var versionNo = currentMaxVersion + 1;
            nextVersionByStory[import.StoryId] = versionNo;
            var version = new TranslationVersion
            {
                TenantId = tenantId,
                StoryId = import.StoryId,
                VersionNo = versionNo,
                Title = import.Title,
                Metadata = import.Metadata,
                CreatedBy = userId,
                CreatedAt = now,
                UpdatedAt = now
            };
            dbContext.TranslationVersions.Add(version);

            foreach (var line in import.Lines)
            {
                dbContext.TranslationLines.Add(new TranslationLine
                {
                    Version = version,
                    SourceLineId = line.SourceLineId,
                    StoryId = import.StoryId,
                    LineNo = line.LineNo,
                    Speaker = line.Speaker,
                    Text = line.Text,
                    Metadata = line.Metadata,
                    CreatedAt = now,
                    UpdatedAt = now
                });
            }

            created.Add(new CreatedImportItem(
                import.StoryType,
                import.ScenarioId,
                import.StoryId,
                version,
                versionNo,
                import.Lines.Count));
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return new PersistImportResult(created
            .Select(item => new ImportedVersion(
                item.StoryType,
                item.ScenarioId,
                item.StoryId,
                item.Version.Id,
                item.VersionNo,
                item.LineCount))
            .ToArray());
    }

    /// <summary>
    /// Requests Search Service to refresh each imported translation version.
    /// </summary>
    private static async Task RefreshImportedVersionsAsync(
        IReadOnlyList<ImportedVersion> importedVersions,
        long tenantId,
        SearchIndexRefreshClient searchIndexRefreshClient,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        var refresh = await searchIndexRefreshClient.RefreshTranslationVersionsAsync(
            tenantId,
            importedVersions.Select(item => item.TranslationVersionId).ToArray(),
            cancellationToken);
        if (!refresh.Success)
        {
            logger.LogError(
                "Translation index refresh failed. translation_version_ids:{TranslationVersionIds} status:{StatusCode} body:{Body}",
                string.Join(",", importedVersions.Select(item => item.TranslationVersionId)),
                refresh.StatusCode,
                refresh.Body);
        }
    }

    /// <summary>
    /// Creates a trace-aware error response for import endpoints.
    /// </summary>
    private static IResult Error(ICurrentRequestContextAccessor contextAccessor, int statusCode, string message)
    {
        var requestContext = contextAccessor.GetCurrent();
        return Results.Json(new ErrorResponse(message, requestContext.TraceId), statusCode: statusCode);
    }

    /// <summary>
    /// Determines whether a request content type is JSON.
    /// </summary>
    private static bool IsJsonContent(string? contentType)
    {
        return contentType?.StartsWith("application/json", StringComparison.OrdinalIgnoreCase) == true;
    }

    /// <summary>
    /// Normalizes optional request text fields before persistence.
    /// </summary>
    private static string? NormalizeOptionalText(string? value)
    {
        var trimmed = value?.Trim();
        return string.IsNullOrWhiteSpace(trimmed) ? null : trimmed;
    }

    /// <summary>
    /// Serializes optional metadata into the jsonb storage representation.
    /// </summary>
    private static string? SerializeMetadata(JsonElement? metadata)
    {
        if (metadata is null || metadata.Value.ValueKind is JsonValueKind.Undefined or JsonValueKind.Null)
        {
            return null;
        }

        return metadata.Value.GetRawText();
    }

    private static bool IsValidMetadata(JsonElement? metadata)
    {
        return metadata is null
            || metadata.Value.ValueKind is JsonValueKind.Undefined or JsonValueKind.Null or JsonValueKind.Object;
    }

    private static bool IsValidVersionMetadata(JsonElement? metadata)
    {
        if (metadata is null || metadata.Value.ValueKind is JsonValueKind.Undefined or JsonValueKind.Null)
        {
            return true;
        }

        if (metadata.Value.ValueKind is not JsonValueKind.Object)
        {
            return false;
        }

        if (!metadata.Value.TryGetProperty("staff", out var staff))
        {
            return true;
        }

        if (staff.ValueKind is JsonValueKind.Null)
        {
            return true;
        }

        if (staff.ValueKind is not JsonValueKind.Object)
        {
            return false;
        }

        return IsValidOptionalStringProperty(staff, "translator")
            && IsValidOptionalStringProperty(staff, "proofreader")
            && IsValidOptionalStringProperty(staff, "approver");
    }

    private static bool IsValidOptionalStringProperty(JsonElement owner, string propertyName)
    {
        return !owner.TryGetProperty(propertyName, out var property)
            || property.ValueKind is JsonValueKind.String or JsonValueKind.Null;
    }

    private static async Task AcquireVersionLockAsync(
        SekaiPlatformDbContext dbContext,
        long tenantId,
        long storyId,
        CancellationToken cancellationToken)
    {
        var tenantPart = ToAdvisoryLockPart(tenantId);
        var storyPart = ToAdvisoryLockPart(storyId);
        await dbContext.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT pg_advisory_xact_lock({tenantPart}, {storyPart})",
            cancellationToken);
    }

    private static int ToAdvisoryLockPart(long value)
    {
        return unchecked((int)(value ^ (value >> 32)));
    }

    private sealed record RequestReadResult(bool Success, TranslationImportRequest? Request);

    private sealed record ValidationResult(bool Success, string? Error, IReadOnlyList<ValidatedImportItem> Items)
    {
        public static ValidationResult Succeeded(IReadOnlyList<ValidatedImportItem> items) => new(true, null, items);

        public static ValidationResult Failed(string error) => new(false, error, []);
    }

    private sealed record ResolvedResult(
        bool Success,
        int StatusCode,
        string? Error,
        IReadOnlyList<ResolvedImportItem> Items)
    {
        public static ResolvedResult Succeeded(IReadOnlyList<ResolvedImportItem> items) => new(true, 200, null, items);

        public static ResolvedResult Failed(int statusCode, string error) => new(false, statusCode, error, []);
    }

    private sealed record ValidatedImportItem(
        string StoryType,
        string ScenarioId,
        string? Title,
        string? Metadata,
        IReadOnlyList<ValidatedImportLine> Lines);

    private sealed record ValidatedImportLine(
        int LineNo,
        string Text,
        string? Speaker,
        string? Metadata);

    private sealed record ResolvedImportItem(
        string StoryType,
        string ScenarioId,
        string? Title,
        string? Metadata,
        long StoryId,
        IReadOnlyList<ResolvedImportLine> Lines);

    private sealed record ResolvedImportLine(
        long SourceLineId,
        int LineNo,
        string Text,
        string? Speaker,
        string? Metadata);

    private sealed record CreatedImportItem(
        string StoryType,
        string ScenarioId,
        long StoryId,
        TranslationVersion Version,
        int VersionNo,
        int LineCount);

    private sealed record PersistImportResult(IReadOnlyList<ImportedVersion> Items);

    private sealed record ImportedVersion(
        string StoryType,
        string ScenarioId,
        long StoryId,
        long TranslationVersionId,
        int VersionNo,
        int LineCount);

}
