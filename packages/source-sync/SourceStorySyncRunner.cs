using Microsoft.EntityFrameworkCore;
using SekaiPlatform.Database;
using SekaiPlatform.SourceSync.Catalog;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace SekaiPlatform.SourceSync;

/// <summary>
/// Runs the source story synchronization workflow against the platform database.
/// </summary>
/// <param name="dbContext">Database context used for sync jobs and story writes.</param>
/// <param name="masterClient">Client used to fetch Moe Sekai master data.</param>
/// <param name="scenarioClient">Client used to download scenario assets.</param>
/// <param name="catalogBuilder">Builder that converts master data into story drafts.</param>
/// <param name="scenarioParser">Parser that extracts source lines from scenario JSON.</param>
/// <param name="options">Moe Sekai source synchronization options.</param>
/// <param name="executionGate">Process-local gate used to reject concurrent sync triggers.</param>
public sealed class SourceStorySyncRunner(
    SekaiPlatformDbContext dbContext,
    MoeSekaiMasterClient masterClient,
    MoeSekaiScenarioClient scenarioClient,
    MoeSekaiCatalogBuilder catalogBuilder,
    UnityScenarioParser scenarioParser,
    MoeSekaiSourceSyncOptions options,
    SourceStorySyncExecutionGate executionGate)
{
    internal const long AdvisoryLockKey = 8210187004;

    /// <summary>
    /// Executes one source story sync job and records its final status.
    /// </summary>
    /// <param name="triggerType">Trigger type stored on the sync job.</param>
    /// <param name="cancellationToken">Token used to cancel the sync workflow.</param>
    /// <returns>The persisted sync job record.</returns>
    public async Task<SyncJob> RunAsync(string triggerType, CancellationToken cancellationToken)
    {
        var result = await RunWithResultAsync(triggerType, cancellationToken);
        return result.Job;
    }

    /// <summary>
    /// Creates a pending source sync job that can be completed by a background runner.
    /// </summary>
    /// <param name="triggerType">Trigger type stored on the sync job.</param>
    /// <param name="cancellationToken">Token used to cancel job creation.</param>
    /// <returns>The persisted pending sync job.</returns>
    public async Task<SyncJob> CreatePendingJobAsync(string triggerType, CancellationToken cancellationToken)
    {
        if (!executionGate.TryReservePendingJob())
        {
            throw new SourceSyncAlreadyRunningException();
        }

        try
        {
            var job = CreatePendingJob(triggerType);
            dbContext.SyncJobs.Add(job);
            await dbContext.SaveChangesAsync(cancellationToken);
            executionGate.BindReservedPendingJob(job.Id);
            return job;
        }
        catch
        {
            executionGate.ReleasePendingReservation();
            throw;
        }
    }

    /// <summary>
    /// Executes one source story sync job and returns the changed stories alongside the job record.
    /// </summary>
    /// <param name="triggerType">Trigger type stored on the sync job.</param>
    /// <param name="cancellationToken">Token used to cancel the sync workflow.</param>
    /// <returns>The persisted sync job record and successfully synchronized story identifiers.</returns>
    public async Task<SourceStorySyncRunResult> RunWithResultAsync(string triggerType, CancellationToken cancellationToken)
    {
        using var executionLease = executionGate.TryEnterNewJob();
        if (executionLease is null)
        {
            throw new SourceSyncAlreadyRunningException();
        }

        var lockAcquired = await TryAcquireSyncLockAsync(cancellationToken);
        if (!lockAcquired)
        {
            throw new SourceSyncAlreadyRunningException();
        }

        SyncJob? job = null;
        IReadOnlyList<long> syncedStoryIds = [];
        try
        {
            job = CreatePendingJob(triggerType);
            dbContext.SyncJobs.Add(job);
            await dbContext.SaveChangesAsync(cancellationToken);

            syncedStoryIds = await ExecuteJobAsync(job, cancellationToken);
        }
        catch (OperationCanceledException) when (job is not null)
        {
            await MarkJobFailedAsync(job, "原文同步已取消。", CancellationToken.None);
        }
        catch (Exception) when (job is not null)
        {
            await MarkJobFailedAsync(job, "原文同步失败。", CancellationToken.None);
        }
        finally
        {
            await ReleaseSyncLockAsync();
        }

        return new SourceStorySyncRunResult(
            job ?? throw new InvalidOperationException("Source story sync job was not created."),
            syncedStoryIds);
    }

    /// <summary>
    /// Executes an already-created pending sync job and records its final status.
    /// </summary>
    /// <param name="syncJobId">Pending sync job identifier.</param>
    /// <param name="cancellationToken">Token used to cancel the sync workflow.</param>
    /// <returns>The persisted sync job record and successfully synchronized story identifiers.</returns>
    public async Task<SourceStorySyncRunResult> RunPendingJobWithResultAsync(
        long syncJobId,
        CancellationToken cancellationToken)
    {
        var executionLease = executionGate.TryEnterPendingJob(syncJobId);
        if (executionLease is null)
        {
            var blockedJob = await dbContext.SyncJobs.FindAsync([syncJobId], cancellationToken)
                ?? throw new InvalidOperationException($"Source story sync job {syncJobId} was not found.");
            await MarkJobFailedAsync(blockedJob, "原文同步任务正在运行。", cancellationToken);
            return new SourceStorySyncRunResult(blockedJob, []);
        }

        using (executionLease)
        {
            var lockAcquired = await TryAcquireSyncLockAsync(cancellationToken);
            SyncJob? job = null;
            IReadOnlyList<long> syncedStoryIds = [];
            try
            {
                job = await dbContext.SyncJobs.FindAsync([syncJobId], cancellationToken)
                    ?? throw new InvalidOperationException($"Source story sync job {syncJobId} was not found.");

                if (!lockAcquired)
                {
                    await MarkJobFailedAsync(job, "原文同步任务正在运行。", cancellationToken);
                    return new SourceStorySyncRunResult(job, syncedStoryIds);
                }

                syncedStoryIds = await ExecuteJobAsync(job, cancellationToken);
            }
            catch (OperationCanceledException) when (job is not null)
            {
                await MarkJobFailedAsync(job, "原文同步已取消。", CancellationToken.None);
            }
            catch (Exception) when (job is not null)
            {
                await MarkJobFailedAsync(job, "原文同步失败。", CancellationToken.None);
            }
            finally
            {
                if (lockAcquired)
                {
                    await ReleaseSyncLockAsync();
                }
            }

            return new SourceStorySyncRunResult(
                job ?? throw new InvalidOperationException($"Source story sync job {syncJobId} was not loaded."),
                syncedStoryIds);
        }
    }

    /// <summary>
    /// Runs the external source sync workflow for an existing job row.
    /// </summary>
    /// <param name="job">Persisted sync job to update.</param>
    /// <param name="cancellationToken">Token used to cancel database and network work.</param>
    /// <returns>Story identifiers whose source lines were replaced.</returns>
    private async Task<IReadOnlyList<long>> ExecuteJobAsync(SyncJob job, CancellationToken cancellationToken)
    {
        job.Status = SourceSyncConstants.StatusRunning;
        job.StartedAt = DateTimeOffset.UtcNow;
        job.UpdatedAt = job.StartedAt.Value;
        await dbContext.SaveChangesAsync(cancellationToken);

        var masterData = await masterClient.FetchAsync(cancellationToken);
        var drafts = catalogBuilder.Build(masterData);
        var character2ds = UnityScenarioParser.BuildCharacter2dMap(masterData.Character2ds);
        var mobCharacters = UnityScenarioParser.BuildMobCharacterMap(masterData.MobCharacters);
        var gameCharacters = UnityScenarioParser.BuildGameCharacterMap(masterData.GameCharacters);

        var results = await SyncDraftsAsync(drafts, character2ds, mobCharacters, gameCharacters, cancellationToken);

        var allScenariosFailed = drafts.Count > 0 && results.SyncedStories == 0 && results.Failures.Count > 0;
        job.Status = allScenariosFailed ? SourceSyncConstants.StatusFailed : SourceSyncConstants.StatusSucceeded;
        job.ErrorMessage = allScenariosFailed ? "原文同步失败。" : null;
        job.EndedAt = DateTimeOffset.UtcNow;
        job.UpdatedAt = job.EndedAt.Value;
        job.Metadata = SourceSyncJson.Serialize(new
        {
            source = SourceSyncConstants.Source,
            version = masterData.Version,
            story_count = drafts.Count,
            synced_story_count = results.SyncedStories,
            skipped_story_count = results.SkippedStories,
            source_line_count = results.SourceLines,
            failed_scenario_count = results.Failures.Count,
            failed_scenario_samples = results.Failures.Take(options.FailureSampleLimit)
        });
        await dbContext.SaveChangesAsync(cancellationToken);

        return results.SyncedStoryIds;
    }

    /// <summary>
    /// Attempts to acquire the PostgreSQL advisory lock used to serialize sync jobs.
    /// </summary>
    /// <param name="cancellationToken">Token used to cancel the lock query.</param>
    /// <returns><see langword="true"/> when the lock was acquired.</returns>
    private async Task<bool> TryAcquireSyncLockAsync(CancellationToken cancellationToken)
    {
        await dbContext.Database.OpenConnectionAsync(cancellationToken);
        var acquired = await dbContext.Database
            .SqlQueryRaw<bool>($"SELECT pg_try_advisory_lock({AdvisoryLockKey}) AS \"Value\"")
            .SingleAsync(cancellationToken);

        if (!acquired)
        {
            await dbContext.Database.CloseConnectionAsync();
        }

        return acquired;
    }

    /// <summary>
    /// Releases the PostgreSQL advisory lock and closes the held connection.
    /// </summary>
    /// <returns>A task that completes after the lock is released.</returns>
    private async Task ReleaseSyncLockAsync()
    {
        await dbContext.Database
            .SqlQueryRaw<bool>($"SELECT pg_advisory_unlock({AdvisoryLockKey}) AS \"Value\"")
            .SingleAsync(CancellationToken.None);
        await dbContext.Database.CloseConnectionAsync();
    }

    /// <summary>
    /// Upserts story groups and stories, then replaces source lines for each downloaded scenario.
    /// </summary>
    /// <param name="drafts">Story synchronization drafts to process.</param>
    /// <param name="character2ds">Character 2D lookup keyed by character2d ID.</param>
    /// <param name="mobCharacters">Mob character names keyed by mob character ID.</param>
    /// <param name="gameCharacters">Game character names keyed by game character ID.</param>
    /// <param name="cancellationToken">Token used to cancel database and network operations.</param>
    /// <returns>Aggregate counts, synchronized story identifiers, and failed scenario samples.</returns>
    private async Task<SyncDraftResults> SyncDraftsAsync(
        IReadOnlyList<StorySyncDraft> drafts,
        IReadOnlyDictionary<int, Character2dInfo> character2ds,
        IReadOnlyDictionary<int, string> mobCharacters,
        IReadOnlyDictionary<int, string> gameCharacters,
        CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        var existingStoryStates = await LoadExistingStoryStatesAsync(cancellationToken);
        var groups = await UpsertGroupsAsync(drafts.Select(item => item.Group), now, cancellationToken);
        var stories = await UpsertStoriesAsync(drafts, groups, now, cancellationToken);

        var syncedStories = 0;
        var skippedStories = 0;
        var sourceLines = 0;
        var syncedStoryIds = new List<long>();
        var failures = new List<ScenarioFailure>();
        var concurrency = Math.Max(1, options.ScenarioDownloadConcurrency);
        var downloads = drafts
            .Where(draft => !CanSkipScenarioDownload(draft, existingStoryStates))
            .ToArray();
        skippedStories = drafts.Count - downloads.Length;

        for (var i = 0; i < downloads.Length; i += concurrency)
        {
            var chunk = downloads.Skip(i).Take(concurrency).ToArray();
            var parsedScenarios = await Task.WhenAll(chunk.Select(draft =>
                DownloadAndParseScenarioAsync(draft, character2ds, mobCharacters, gameCharacters, cancellationToken)));

            foreach (var parsedScenario in parsedScenarios)
            {
                if (parsedScenario.Failure is not null)
                {
                    failures.Add(parsedScenario.Failure);
                    continue;
                }

                var draft = parsedScenario.Draft;
                var story = stories[$"{draft.Story.StoryType}:{draft.Story.ScenarioId}"];
                var lines = parsedScenario.Lines;

                await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
                await dbContext.StorySourceLines
                    .Where(line => line.StoryId == story.Id)
                    .ExecuteDeleteAsync(cancellationToken);

                var lineTimestamp = DateTimeOffset.UtcNow;
                dbContext.StorySourceLines.AddRange(lines.Select(line => new StorySourceLine
                {
                    StoryId = story.Id,
                    LineNo = line.LineNo,
                    LineType = line.LineType,
                    Speaker = line.Speaker,
                    Text = line.Text,
                    Metadata = line.Metadata,
                    CreatedAt = lineTimestamp,
                    UpdatedAt = lineTimestamp
                }));
                await dbContext.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
                syncedStories++;
                syncedStoryIds.Add(story.Id);
                sourceLines += lines.Count;
            }
        }

        return new SyncDraftResults(syncedStories, skippedStories, sourceLines, syncedStoryIds, failures);
    }

    /// <summary>
    /// Loads the persisted story metadata and whether source lines already exist before upserting master records.
    /// </summary>
    /// <param name="cancellationToken">Token used to cancel database reads.</param>
    /// <returns>Existing story state keyed by story type and scenario ID.</returns>
    private async Task<IReadOnlyDictionary<string, ExistingStoryState>> LoadExistingStoryStatesAsync(
        CancellationToken cancellationToken)
    {
        var existingStories = await dbContext.Stories
            .Select(story => new
            {
                story.Id,
                story.StoryType,
                story.ScenarioId,
                story.Metadata
            })
            .ToArrayAsync(cancellationToken);

        if (existingStories.Length == 0)
        {
            return new Dictionary<string, ExistingStoryState>(StringComparer.Ordinal);
        }

        var storiesWithSourceLines = await dbContext.StorySourceLines
            .Select(line => line.StoryId)
            .Distinct()
            .ToArrayAsync(cancellationToken);
        var storyIdsWithSourceLines = storiesWithSourceLines.ToHashSet();

        return existingStories.ToDictionary(
            story => $"{story.StoryType}:{story.ScenarioId}",
            story => new ExistingStoryState(story.Metadata, storyIdsWithSourceLines.Contains(story.Id)),
            StringComparer.Ordinal);
    }

    /// <summary>
    /// Determines whether a scenario asset can be skipped because the story metadata and source lines already exist.
    /// </summary>
    /// <param name="draft">Story draft built from the current master data.</param>
    /// <param name="existingStoryStates">Persisted story state before this sync run updates master metadata.</param>
    /// <returns><see langword="true"/> when the scenario download is unchanged and can be skipped.</returns>
    private static bool CanSkipScenarioDownload(
        StorySyncDraft draft,
        IReadOnlyDictionary<string, ExistingStoryState> existingStoryStates)
    {
        var key = $"{draft.Story.StoryType}:{draft.Story.ScenarioId}";
        return existingStoryStates.TryGetValue(key, out var state)
            && state.HasSourceLines
            && JsonMetadataEquals(state.Metadata, draft.Story.Metadata);
    }

    /// <summary>
    /// Compares JSON metadata semantically because PostgreSQL jsonb does not preserve property order.
    /// </summary>
    /// <param name="storedMetadata">Metadata already stored in the database.</param>
    /// <param name="draftMetadata">Metadata generated from the current master data.</param>
    /// <returns><see langword="true"/> when both metadata payloads describe the same source state.</returns>
    private static bool JsonMetadataEquals(string? storedMetadata, string draftMetadata)
    {
        if (string.IsNullOrWhiteSpace(storedMetadata))
        {
            return false;
        }

        try
        {
            return JsonNode.DeepEquals(JsonNode.Parse(storedMetadata), JsonNode.Parse(draftMetadata));
        }
        catch (JsonException)
        {
            return false;
        }
    }

    /// <summary>
    /// Downloads and parses one scenario without touching the database context.
    /// </summary>
    /// <param name="draft">Story draft that describes the scenario to download.</param>
    /// <param name="character2ds">Character 2D lookup keyed by character2d ID.</param>
    /// <param name="mobCharacters">Mob character names keyed by mob character ID.</param>
    /// <param name="gameCharacters">Game character names keyed by game character ID.</param>
    /// <param name="cancellationToken">Token used to cancel scenario download and parsing.</param>
    /// <returns>Parsed source lines or a stable failure sample.</returns>
    private async Task<ParsedScenario> DownloadAndParseScenarioAsync(
        StorySyncDraft draft,
        IReadOnlyDictionary<int, Character2dInfo> character2ds,
        IReadOnlyDictionary<int, string> mobCharacters,
        IReadOnlyDictionary<int, string> gameCharacters,
        CancellationToken cancellationToken)
    {
        try
        {
            var downloaded = await scenarioClient.DownloadAsync(draft.Download, cancellationToken);
            var lines = scenarioParser.Parse(downloaded.Json, character2ds, mobCharacters, gameCharacters);
            if (lines.Count == 0)
            {
                throw new InvalidOperationException("Scenario did not produce source lines.");
            }

            return new ParsedScenario(draft, lines, null);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            return new ParsedScenario(
                draft,
                [],
                new ScenarioFailure(
                    draft.Story.StoryType,
                    draft.Story.ScenarioId,
                    "剧情下载或解析失败。"));
        }
    }

    /// <summary>
    /// Creates a new pending sync job entity with source metadata.
    /// </summary>
    /// <param name="triggerType">Trigger type stored on the sync job.</param>
    /// <returns>A new unsaved sync job entity.</returns>
    private static SyncJob CreatePendingJob(string triggerType)
    {
        var now = DateTimeOffset.UtcNow;
        return new SyncJob
        {
            JobType = SourceSyncConstants.JobType,
            TriggerType = triggerType,
            Status = SourceSyncConstants.StatusPending,
            Metadata = SourceSyncJson.Serialize(new { source = SourceSyncConstants.Source }),
            CreatedAt = now,
            UpdatedAt = now
        };
    }

    /// <summary>
    /// Marks a sync job as failed with stable source metadata.
    /// </summary>
    /// <param name="job">Persisted sync job to update.</param>
    /// <param name="errorMessage">Failure message stored on the job.</param>
    /// <param name="cancellationToken">Token used to cancel the database update.</param>
    private async Task MarkJobFailedAsync(SyncJob job, string errorMessage, CancellationToken cancellationToken)
    {
        job.Status = SourceSyncConstants.StatusFailed;
        job.EndedAt = DateTimeOffset.UtcNow;
        job.UpdatedAt = job.EndedAt.Value;
        job.ErrorMessage = errorMessage;
        job.Metadata = SourceSyncJson.Serialize(new
        {
            source = SourceSyncConstants.Source
        });
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Upserts story groups from draft group metadata.
    /// </summary>
    /// <param name="groupDrafts">Draft groups to upsert.</param>
    /// <param name="now">Timestamp applied to updated group records.</param>
    /// <param name="cancellationToken">Token used to cancel database operations.</param>
    /// <returns>Story groups keyed by story type, external type, and external ID.</returns>
    private async Task<IReadOnlyDictionary<string, StoryGroup>> UpsertGroupsAsync(
        IEnumerable<StoryGroupDraft> groupDrafts,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var existing = await dbContext.StoryGroups.ToListAsync(cancellationToken);
        var groups = existing.ToDictionary(
            item => $"{item.StoryType}:{item.ExternalType}:{item.ExternalId}",
            StringComparer.Ordinal);

        foreach (var draft in groupDrafts.DistinctBy(item => $"{item.StoryType}:{item.ExternalType}:{item.ExternalId}"))
        {
            var key = $"{draft.StoryType}:{draft.ExternalType}:{draft.ExternalId}";
            if (!groups.TryGetValue(key, out var group))
            {
                group = new StoryGroup
                {
                    StoryType = draft.StoryType,
                    ExternalType = draft.ExternalType,
                    ExternalId = draft.ExternalId,
                    CreatedAt = now
                };
                dbContext.StoryGroups.Add(group);
                groups[key] = group;
            }

            group.DisplayNo = draft.DisplayNo;
            group.Title = draft.Title;
            group.Subtitle = draft.Subtitle;
            group.Metadata = draft.Metadata;
            group.DeletedAt = null;
            group.UpdatedAt = now;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return groups;
    }

    /// <summary>
    /// Upserts stories from draft story metadata and assigns them to existing groups.
    /// </summary>
    /// <param name="drafts">Story synchronization drafts to upsert.</param>
    /// <param name="groups">Persisted story groups keyed by draft group identity.</param>
    /// <param name="now">Timestamp applied to updated story records.</param>
    /// <param name="cancellationToken">Token used to cancel database operations.</param>
    /// <returns>Stories keyed by story type and scenario ID.</returns>
    private async Task<IReadOnlyDictionary<string, Story>> UpsertStoriesAsync(
        IReadOnlyList<StorySyncDraft> drafts,
        IReadOnlyDictionary<string, StoryGroup> groups,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var existing = await dbContext.Stories.ToListAsync(cancellationToken);
        var stories = existing.ToDictionary(
            item => $"{item.StoryType}:{item.ScenarioId}",
            StringComparer.Ordinal);

        foreach (var draft in drafts)
        {
            var key = $"{draft.Story.StoryType}:{draft.Story.ScenarioId}";
            if (!stories.TryGetValue(key, out var story))
            {
                story = new Story
                {
                    StoryType = draft.Story.StoryType,
                    ScenarioId = draft.Story.ScenarioId,
                    CreatedAt = now
                };
                dbContext.Stories.Add(story);
                stories[key] = story;
            }

            var groupKey = $"{draft.Group.StoryType}:{draft.Group.ExternalType}:{draft.Group.ExternalId}";
            story.GroupId = groups[groupKey].Id;
            story.Title = draft.Story.Title;
            story.SortOrder = draft.Story.SortOrder;
            story.Metadata = draft.Story.Metadata;
            story.DeletedAt = null;
            story.UpdatedAt = now;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return stories;
    }
}

/// <summary>
/// Failed scenario sample stored in source sync job metadata.
/// </summary>
/// <param name="StoryType">Platform story type constant.</param>
/// <param name="ScenarioId">Moe Sekai scenario ID.</param>
/// <param name="ErrorMessage">Stable failure message for operators.</param>
public sealed record ScenarioFailure(
    string StoryType,
    string ScenarioId,
    string ErrorMessage);

/// <summary>
/// Aggregate result from processing story synchronization drafts.
/// </summary>
/// <param name="SyncedStories">Number of stories whose source lines were replaced.</param>
/// <param name="SourceLines">Number of source lines inserted.</param>
/// <param name="SyncedStoryIds">Story identifiers whose source lines were replaced.</param>
/// <param name="Failures">Failed scenario samples.</param>
internal sealed record SyncDraftResults(
    int SyncedStories,
    int SkippedStories,
    int SourceLines,
    IReadOnlyList<long> SyncedStoryIds,
    IReadOnlyList<ScenarioFailure> Failures);

/// <summary>
/// Existing persisted story state used to decide whether scenario content can be skipped.
/// </summary>
/// <param name="Metadata">Story metadata from the previous successful master sync.</param>
/// <param name="HasSourceLines">Whether this story already has parsed source lines.</param>
internal sealed record ExistingStoryState(string? Metadata, bool HasSourceLines);

/// <summary>
/// Result of downloading and parsing one scenario before database writes.
/// </summary>
/// <param name="Draft">Story draft that produced this result.</param>
/// <param name="Lines">Parsed source lines, or an empty list when parsing failed.</param>
/// <param name="Failure">Failure sample when the scenario could not be synchronized.</param>
internal sealed record ParsedScenario(
    StorySyncDraft Draft,
    IReadOnlyList<SourceLineDraft> Lines,
    ScenarioFailure? Failure);

/// <summary>
/// Result of running a source story synchronization workflow.
/// </summary>
/// <param name="Job">Persisted sync job record.</param>
/// <param name="SyncedStoryIds">Story identifiers whose source lines were successfully replaced.</param>
public sealed record SourceStorySyncRunResult(
    SyncJob Job,
    IReadOnlyList<long> SyncedStoryIds);

/// <summary>
/// Indicates that another source story sync job already holds the advisory lock.
/// </summary>
public sealed class SourceSyncAlreadyRunningException()
    : Exception("Source story sync is already running.");
