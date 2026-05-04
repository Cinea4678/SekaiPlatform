using System.Text.RegularExpressions;

namespace SekaiPlatform.SourceSync;

/// <summary>
/// Validates configured Moe Sekai URLs and asset path segments before requests are made.
/// </summary>
internal static partial class MoeSekaiUrlSafety
{
    /// <summary>
    /// Validates all configured Moe Sekai URL lists against scheme and host rules.
    /// </summary>
    /// <param name="options">Moe Sekai synchronization options to validate.</param>
    public static void ValidateOptions(MoeSekaiSourceSyncOptions options)
    {
        foreach (var url in options.VersionUrls.Concat(options.MasterBaseUrls).Concat(options.AssetBaseUrls))
        {
            ValidateBaseUrl(url, options);
        }
    }

    /// <summary>
    /// Validates and escapes a single asset path segment.
    /// </summary>
    /// <param name="value">Raw path segment from master data.</param>
    /// <param name="fieldName">Field name used in validation errors.</param>
    /// <param name="storyType">Story type used in validation errors.</param>
    /// <param name="scenarioId">Scenario ID used in validation errors.</param>
    /// <returns>The escaped path segment.</returns>
    public static string RequirePathSegment(string? value, string fieldName, string storyType, string scenarioId)
    {
        if (string.IsNullOrWhiteSpace(value) || !SafePathSegmentRegex().IsMatch(value))
        {
            throw new InvalidOperationException(
                $"Invalid {fieldName} for scenario {storyType}:{scenarioId}.");
        }

        return Uri.EscapeDataString(value);
    }

    /// <summary>
    /// Validates that a configured base URL is absolute, allowed, and uses an approved scheme.
    /// </summary>
    /// <param name="url">Configured base URL.</param>
    /// <param name="options">Options providing security policy.</param>
    private static void ValidateBaseUrl(string url, MoeSekaiSourceSyncOptions options)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            throw new InvalidOperationException("Moe Sekai URL configuration must be absolute.");
        }

        if (uri.Scheme != Uri.UriSchemeHttps && !(options.AllowInsecureHttp && uri.Scheme == Uri.UriSchemeHttp))
        {
            throw new InvalidOperationException("Moe Sekai URL configuration must use HTTPS.");
        }

        if (!options.AllowedHosts.Contains(uri.Host, StringComparer.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Moe Sekai URL host is not allowed.");
        }
    }

    /// <summary>
    /// Matches safe Moe Sekai asset path segment characters.
    /// </summary>
    /// <returns>A compiled safe path segment regular expression.</returns>
    [GeneratedRegex("^[A-Za-z0-9_.-]+$")]
    private static partial Regex SafePathSegmentRegex();
}
