namespace SekaiPlatform.Shared.Web.Auth;

/// <summary>
/// Provides JWT authentication settings shared by platform services.
/// </summary>
public sealed class SekaiJwtOptions
{
    /// <summary>
    /// Configuration section name for JWT settings.
    /// </summary>
    public const string SectionName = "Jwt";

    /// <summary>
    /// Gets the expected token issuer.
    /// </summary>
    public string Issuer { get; init; } = string.Empty;

    /// <summary>
    /// Gets the expected token audience.
    /// </summary>
    public string Audience { get; init; } = string.Empty;

    /// <summary>
    /// Gets the symmetric signing key used to validate access tokens.
    /// </summary>
    public string SigningKey { get; init; } = string.Empty;

    /// <summary>
    /// Gets the access token lifetime in minutes.
    /// </summary>
    public int AccessTokenLifetimeMinutes { get; init; } = 7 * 24 * 60;
}
