using Microsoft.Extensions.Configuration;

namespace SekaiPlatform.Shared.Web.Search;

/// <summary>
/// Configures internal search index refresh calls after data-changing workflows.
/// </summary>
public sealed class SearchIndexRefreshOptions
{
    /// <summary>
    /// Configuration section name for search index refresh options.
    /// </summary>
    public const string SectionName = "SearchIndexRefresh";

    /// <summary>
    /// Gets or sets the HTTP timeout applied to each refresh request batch.
    /// </summary>
    public TimeSpan RequestTimeout { get; set; } = TimeSpan.FromMinutes(2);

    /// <summary>
    /// Gets or sets how many story identifiers are sent in one refresh request.
    /// </summary>
    public int StoryBatchSize { get; set; } = 200;

    /// <summary>
    /// Gets or sets how many translation version identifiers are sent in one refresh request.
    /// </summary>
    public int TranslationVersionBatchSize { get; set; } = 200;

    /// <summary>
    /// Reads search index refresh options from configuration with bounded fallbacks.
    /// </summary>
    /// <param name="configuration">Configuration section containing refresh settings.</param>
    /// <returns>Validated refresh options.</returns>
    public static SearchIndexRefreshOptions FromConfiguration(IConfiguration configuration)
    {
        var options = new SearchIndexRefreshOptions();
        if (TimeSpan.TryParse(configuration[nameof(RequestTimeout)], out var timeout) && timeout > TimeSpan.Zero)
        {
            options.RequestTimeout = timeout;
        }

        if (int.TryParse(configuration[nameof(StoryBatchSize)], out var storyBatchSize) && storyBatchSize > 0)
        {
            options.StoryBatchSize = storyBatchSize;
        }

        if (int.TryParse(configuration[nameof(TranslationVersionBatchSize)], out var translationVersionBatchSize)
            && translationVersionBatchSize > 0)
        {
            options.TranslationVersionBatchSize = translationVersionBatchSize;
        }

        return options;
    }
}
