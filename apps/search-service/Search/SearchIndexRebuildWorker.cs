namespace SekaiPlatform.SearchService.Search;

/// <summary>
/// Runs queued search index rebuild requests outside the HTTP request lifecycle.
/// </summary>
internal sealed class SearchIndexRebuildWorker(
    SearchIndexRebuildQueue queue,
    IServiceScopeFactory scopeFactory,
    ILogger<SearchIndexRebuildWorker> logger) : BackgroundService
{
    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var item in queue.ReadAllAsync(stoppingToken))
        {
            await RunRebuildAsync(item, stoppingToken);
        }
    }

    private async Task RunRebuildAsync(SearchIndexRebuildWorkItem item, CancellationToken stoppingToken)
    {
        using var logScope = logger.BeginScope(
            "search_rebuild_job_id:{SearchRebuildJobId} trace_id:{TraceId}",
            item.JobId,
            item.TraceId);
        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var rebuilder = scope.ServiceProvider.GetRequiredService<SearchIndexRebuilder>();
            var response = await rebuilder.RebuildAsync(item.Request, stoppingToken);
            logger.LogInformation(
                "Search index rebuild completed. scope:{Scope} source_indexed:{SourceIndexed} translation_indexed:{TranslationIndexed}",
                response.Scope,
                response.SourceIndexed,
                response.TranslationIndexed);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            logger.LogWarning("Search index rebuild stopped before completion. scope:{Scope}", item.Scope);
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Search index rebuild failed. scope:{Scope}", item.Scope);
        }
    }
}
