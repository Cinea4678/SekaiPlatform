namespace SekaiPlatform.SearchService.Search;

/// <summary>
/// Elasticsearch connection and index settings used by the language asset index.
/// </summary>
public sealed class SearchIndexOptions
{
    /// <summary>
    /// Configuration section name for search index options.
    /// </summary>
    public const string SectionName = "Elasticsearch";

    /// <summary>
    /// Gets or sets the Elasticsearch base URL.
    /// </summary>
    public string Url { get; set; } = "http://localhost:9200";

    /// <summary>
    /// Gets or sets the unified language asset index name.
    /// </summary>
    public string IndexName { get; set; } = "sekai-language-assets-v1";

    /// <summary>
    /// Gets or sets the number of documents sent per bulk indexing request.
    /// </summary>
    public int BulkBatchSize { get; set; } = 1000;
}
