using System.Threading.Channels;

namespace SekaiPlatform.SearchService.Search;

/// <summary>
/// Queues validated search index rebuild requests for asynchronous execution.
/// </summary>
internal sealed class SearchIndexRebuildQueue
{
    private const int Capacity = 100;
    private readonly Channel<SearchIndexRebuildWorkItem> channel = Channel.CreateBounded<SearchIndexRebuildWorkItem>(
        new BoundedChannelOptions(Capacity)
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = true,
            SingleWriter = false
        });

    /// <summary>
    /// Attempts to enqueue one validated search index rebuild request.
    /// </summary>
    /// <param name="item">The rebuild work item to run in the background.</param>
    /// <returns>Whether the work item was accepted by the queue.</returns>
    public bool TryEnqueue(SearchIndexRebuildWorkItem item)
    {
        return channel.Writer.TryWrite(item);
    }

    /// <summary>
    /// Reads queued rebuild work items until the service stops.
    /// </summary>
    /// <param name="cancellationToken">Token that stops queue consumption.</param>
    /// <returns>An asynchronous sequence of queued rebuild work items.</returns>
    public IAsyncEnumerable<SearchIndexRebuildWorkItem> ReadAllAsync(CancellationToken cancellationToken)
    {
        return channel.Reader.ReadAllAsync(cancellationToken);
    }
}

/// <summary>
/// Describes one accepted search index rebuild request.
/// </summary>
/// <param name="JobId">Identifier returned to the caller for log correlation.</param>
/// <param name="Scope">Normalized rebuild scope.</param>
/// <param name="TraceId">Request trace identifier captured when the rebuild was accepted.</param>
/// <param name="Request">Validated rebuild request payload.</param>
internal sealed record SearchIndexRebuildWorkItem(
    Guid JobId,
    string Scope,
    string TraceId,
    SearchIndexRebuildRequest Request);
