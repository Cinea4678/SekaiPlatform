namespace SekaiPlatform.Shared.Web;

public sealed record CurrentRequestContext(
    string TraceId,
    long? UserId,
    long? TenantId);
