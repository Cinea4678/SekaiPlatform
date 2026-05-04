namespace SekaiPlatform.Shared.Web;

/// <summary>
/// Defines platform-specific headers used for tracing and internal request context.
/// </summary>
public static class SekaiHeaders
{
    /// <summary>
    /// Header carrying the readable platform trace identifier.
    /// </summary>
    public const string TraceId = "X-Sekai-Trace-Id";

    /// <summary>
    /// Header carrying the authenticated user identifier on trusted internal calls.
    /// </summary>
    public const string UserId = "X-Sekai-User-Id";

    /// <summary>
    /// Header carrying the selected tenant identifier on trusted internal calls.
    /// </summary>
    public const string TenantId = "X-Sekai-Tenant-Id";
}
