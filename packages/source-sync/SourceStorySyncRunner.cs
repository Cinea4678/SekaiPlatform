using Microsoft.EntityFrameworkCore;
using SekaiPlatform.Database;

namespace SekaiPlatform.SourceSync;

/// <summary>
/// Runs the Phase 4 source story synchronization workflow against the platform database.
/// </summary>
/// <param name="dbContext">Database context used for sync jobs and story writes.</param>
/// <param name="masterClient">Client used to fetch Moe Sekai master data.</param>
/// <param name="scenarioClient">Client used to download scenario assets.</param>
/// <param name="catalogBuilder">Builder that converts master data into story drafts.</param>
/// <param name="scenarioParser">Parser that extracts source lines from scenario JSON.</param>
/// <param name="options">Moe Sekai source synchronization options.</param>
public sealed class SourceStorySyncRunner(
    SekaiPlatformDbContext dbContext,
    MoeSekaiMasterClient masterClient,
    MoeSekaiScenarioClient scenarioClient,
    MoeSekaiCatalogBuilder catalogBuilder,
    UnityScenarioParser scenarioParser,
    MoeSekaiSourceSyncOptions options)
{
    private const long AdvisoryLockKey = 8210187004;

    /// <summary>
    /// Executes one source story sync job and records its final status.
    /// </summary>
    /// <param name="triggerType">Trigger type stored on the sync job.</param>
    /// <param name="cancellationToken">Token used to cancel the sync workflow.</param>
    /// <returns>The persisted sync job record.</returns>
    public async Task<SyncJob> RunAsync(string triggerType, CancellationToken cancellationToken)
    {
        var lockAcquired = await TryAcquireSyncLockAsync(cancellationToken);
        if (!lockAcquired)
        {
            throw new SourceSyncAlreadyRunningException();
        }

        SyncJob? job = null;
        try
        {
            var now = DateTimeOffset.UtcNow;
            job = new SyncJob
            {
                JobType = SourceSyncConstants.JobType,
                TriggerType = triggerType,
                Status = SourceSyncConstants.StatusPending,
                CreatedAt = now,
                UpdatedAt = now
            };
            dbContext.SyncJobs.Add(job);
            await dbContext.SaveChangesAsync(cancellationToken);

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
            job.ErrorMessage = allScenariosFailed ? "Source story sync failed." : null;
            job.EndedAt = DateTimeOffset.UtcNow;
            job.UpdatedAt = job.EndedAt.Value;
            job.Metadata = SourceSyncJson.Serialize(new
            {
                source = SourceSyncConstants.Source,
                version = masterData.Version,
                story_count = drafts.Count,
                synced_story_count = results.SyncedStories,
                source_line_count = results.SourceLines,
                failed_scenario_count = results.Failures.Count,
                failed_scenario_samples = results.Failures.Take(options.FailureSampleLimit)
            });
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (OperationCanceledException) when (job is not null)
        {
            job.Status = SourceSyncConstants.StatusFailed;
            job.EndedAt = DateTimeOffset.UtcNow;
            job.UpdatedAt = job.EndedAt.Value;
            job.ErrorMessage = "Source story sync canceled.";
            job.Metadata = SourceSyncJson.Serialize(new
            {
                source = SourceSyncConstants.Source
            });
            await dbContext.SaveChangesAsync(CancellationToken.None);
        }
        catch (Exception) when (job is not null)
        {
            job.Status = SourceSyncConstants.StatusFailed;
            job.EndedAt = DateTimeOffset.UtcNow;
            job.UpdatedAt = job.EndedAt.Value;
            job.ErrorMessage = "Source story sync failed.";
            job.Metadata = SourceSyncJson.Serialize(new
            {
                source = SourceSyncConstants.Source
            });
            await dbContext.SaveChangesAsync(CancellationToken.None);
        }
        finally
        {
            await ReleaseSyncLockAsync();
        }

        return job ?? throw new InvalidOperationException("Source story sync job was not created.");
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
    /// <returns>Aggregate counts and failed scenario samples.</returns>
    private async Task<SyncDraftResults> SyncDraftsAsync(
        IReadOnlyList<StorySyncDraft> drafts,
        IReadOnlyDictionary<int, Character2dInfo> character2ds,
        IReadOnlyDictionary<int, string> mobCharacters,
        IReadOnlyDictionary<int, string> gameCharacters,
        CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        var groups = await UpsertGroupsAsync(drafts.Select(item => item.Group), now, cancellationToken);
        var stories = await UpsertStoriesAsync(drafts, groups, now, cancellationToken);

        var syncedStories = 0;
        var sourceLines = 0;
        var failures = new List<ScenarioFailure>();
        foreach (var draft in drafts)
        {
            var story = stories[$"{draft.Story.StoryType}:{draft.Story.ScenarioId}"];
            IReadOnlyList<SourceLineDraft> lines;
            try
            {
                var downloaded = await scenarioClient.DownloadAsync(draft.Download, cancellationToken);
                lines = scenarioParser.Parse(downloaded.Json, character2ds, mobCharacters, gameCharacters);
                if (lines.Count == 0)
                {
                    throw new InvalidOperationException("Scenario did not produce source lines.");
                }
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                failures.Add(new ScenarioFailure(
                    draft.Story.StoryType,
                    draft.Story.ScenarioId,
                    "Scenario download or parse failed."));
                continue;
            }

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
            sourceLines += lines.Count;
        }

        return new SyncDraftResults(syncedStories, sourceLines, failures);
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
/// <param name="Failures">Failed scenario samples.</param>
internal sealed record SyncDraftResults(
    int SyncedStories,
    int SourceLines,
    IReadOnlyList<ScenarioFailure> Failures);

/// <summary>
/// Indicates that another source story sync job already holds the advisory lock.
/// </summary>
public sealed class SourceSyncAlreadyRunningException()
    : Exception("Source story sync is already running.");
