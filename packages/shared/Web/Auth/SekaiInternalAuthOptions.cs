namespace SekaiPlatform.Shared.Web;

/// <summary>
/// Provides non-symmetric service-to-service token settings.
/// </summary>
public sealed class SekaiInternalAuthOptions
{
    /// <summary>
    /// Configuration section name for internal authentication settings.
    /// </summary>
    public const string SectionName = "InternalAuth";

    /// <summary>
    /// Gets the internal token issuer.
    /// </summary>
    public string Issuer { get; init; } = string.Empty;

    /// <summary>
    /// Gets the current service actor name and inbound token audience.
    /// </summary>
    public string Actor { get; init; } = string.Empty;

    /// <summary>
    /// Gets the base64 PKCS#8 private key used by this actor to sign outbound tokens.
    /// </summary>
    public string PrivateKeyPkcs8 { get; init; } = string.Empty;

    /// <summary>
    /// Gets the outbound internal token lifetime in minutes.
    /// </summary>
    public int TokenLifetimeMinutes { get; init; } = 5;

    /// <summary>
    /// Gets base64 SubjectPublicKeyInfo public keys by actor name.
    /// </summary>
    public Dictionary<string, string> PublicKeys { get; init; } = [];
}
