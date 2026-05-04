namespace SekaiPlatform.Shared.Web;

/// <summary>
/// Carries trace, user, and tenant context for the current request.
/// </summary>
/// <param name="TraceId">The request trace identifier used for logs and error responses.</param>
/// <param name="UserId">The authenticated platform user identifier, when available.</param>
/// <param name="TenantId">The selected tenant identifier, when available.</param>
public sealed record CurrentRequestContext(
    string TraceId,
    long? UserId,
    long? TenantId);
