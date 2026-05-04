namespace SekaiPlatform.Shared.Web;

/// <summary>
/// Configuration for protected search index maintenance calls between internal services.
/// </summary>
public sealed class SearchIndexMaintenanceOptions
{
    /// <summary>
    /// Configuration section name for search index maintenance options.
    /// </summary>
    public const string SectionName = "SearchIndex";

    /// <summary>
    /// Gets or sets the token required by Search Service maintenance endpoints.
    /// </summary>
    public string MaintenanceToken { get; set; } = string.Empty;
}
