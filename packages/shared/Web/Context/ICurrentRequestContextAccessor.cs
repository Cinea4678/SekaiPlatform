namespace SekaiPlatform.Shared.Web.Context;

/// <summary>
/// Provides access to normalized request context values.
/// </summary>
public interface ICurrentRequestContextAccessor
{
    /// <summary>
    /// Gets the current trace, user, and tenant context.
    /// </summary>
    /// <returns>The normalized request context.</returns>
    CurrentRequestContext GetCurrent();
}
