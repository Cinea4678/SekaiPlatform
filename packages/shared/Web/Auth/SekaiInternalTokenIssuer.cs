using System.Globalization;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace SekaiPlatform.Shared.Web;

/// <summary>
/// Issues short-lived internal tokens for service-to-service calls.
/// </summary>
/// <param name="options">Internal token signing and actor options.</param>
public sealed class SekaiInternalTokenIssuer(IOptions<SekaiInternalAuthOptions> options)
{
    private readonly Lazy<SigningCredentials> signingCredentials = new(() =>
    {
        var value = options.Value;
        if (string.IsNullOrWhiteSpace(value.PrivateKeyPkcs8))
        {
            throw new InvalidOperationException("InternalAuth:PrivateKeyPkcs8 must be configured to issue internal tokens.");
        }

        var rsa = RSA.Create();
        rsa.ImportPkcs8PrivateKey(Convert.FromBase64String(value.PrivateKeyPkcs8), out _);
        var key = new RsaSecurityKey(rsa) { KeyId = value.Actor };
        return new SigningCredentials(key, SecurityAlgorithms.RsaSha256);
    });

    /// <summary>
    /// Creates a signed token for an internal service call.
    /// </summary>
    /// <param name="audience">Target service audience.</param>
    /// <param name="scope">Internal capability granted to the call.</param>
    /// <param name="subjectUserId">Delegated user identifier for user-proxy calls.</param>
    /// <param name="tenantId">Delegated tenant identifier for tenant-scoped user-proxy calls.</param>
    /// <returns>A compact JWT signed by the configured actor private key.</returns>
    public string Issue(
        string audience,
        string scope,
        long? subjectUserId = null,
        long? tenantId = null)
    {
        var value = options.Value;
        var now = DateTimeOffset.UtcNow;
        var expiresAt = now.AddMinutes(Math.Clamp(value.TokenLifetimeMinutes, 1, 60));
        var claims = new List<Claim>
        {
            new(SekaiInternalAuthDefaults.ActorClaimType, value.Actor),
            new(SekaiInternalAuthDefaults.ScopeClaimType, scope),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString("N")),
            new(
                JwtRegisteredClaimNames.Iat,
                now.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture),
                ClaimValueTypes.Integer64)
        };

        if (subjectUserId is not null)
        {
            claims.Add(new Claim(
                SekaiInternalAuthDefaults.SubjectUserIdClaimType,
                subjectUserId.Value.ToString(CultureInfo.InvariantCulture)));
        }

        if (tenantId is not null)
        {
            claims.Add(new Claim(
                SekaiAuthDefaults.TenantIdClaimType,
                tenantId.Value.ToString(CultureInfo.InvariantCulture)));
        }

        var token = new JwtSecurityToken(
            value.Issuer,
            audience,
            claims,
            expires: expiresAt.UtcDateTime,
            signingCredentials: signingCredentials.Value);
        token.Header["kid"] = value.Actor;

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
