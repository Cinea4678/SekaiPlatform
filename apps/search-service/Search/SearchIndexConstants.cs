namespace SekaiPlatform.SearchService.Search;

/// <summary>
/// Defines stable field values and request scopes used by the unified search index.
/// </summary>
internal static class SearchIndexConstants
{
    /// <summary>
    /// Asset type used by shared original story line documents.
    /// </summary>
    public const string AssetTypeSource = "source";

    /// <summary>
    /// Asset type used by tenant-owned translation line documents.
    /// </summary>
    public const string AssetTypeTranslation = "translation";

    /// <summary>
    /// Rebuild scope covering both source and translation documents.
    /// </summary>
    public const string ScopeAll = "all";

    /// <summary>
    /// Rebuild scope covering source documents only.
    /// </summary>
    public const string ScopeSource = "source";

    /// <summary>
    /// Rebuild scope covering translation documents only.
    /// </summary>
    public const string ScopeTranslation = "translation";
}
