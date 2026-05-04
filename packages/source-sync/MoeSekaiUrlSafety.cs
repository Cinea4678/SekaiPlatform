using System.Text.RegularExpressions;

namespace SekaiPlatform.SourceSync;

internal static partial class MoeSekaiUrlSafety
{
    public static void ValidateOptions(MoeSekaiSourceSyncOptions options)
    {
        foreach (var url in options.VersionUrls.Concat(options.MasterBaseUrls).Concat(options.AssetBaseUrls))
        {
            ValidateBaseUrl(url, options);
        }
    }

    public static string RequirePathSegment(string? value, string fieldName, string storyType, string scenarioId)
    {
        if (string.IsNullOrWhiteSpace(value) || !SafePathSegmentRegex().IsMatch(value))
        {
            throw new InvalidOperationException(
                $"Invalid {fieldName} for scenario {storyType}:{scenarioId}.");
        }

        return Uri.EscapeDataString(value);
    }

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

    [GeneratedRegex("^[A-Za-z0-9_.-]+$")]
    private static partial Regex SafePathSegmentRegex();
}
