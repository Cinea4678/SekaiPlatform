namespace SekaiPlatform.Shared.Web;

/// <summary>
/// Selects the token model used by a web service authentication pipeline.
/// </summary>
public enum SekaiAuthenticationMode
{
    /// <summary>
    /// Validates frontend user access tokens.
    /// </summary>
    ExternalJwt,

    /// <summary>
    /// Validates service-to-service internal tokens.
    /// </summary>
    InternalToken
}
