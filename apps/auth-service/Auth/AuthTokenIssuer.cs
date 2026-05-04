using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using SekaiPlatform.Shared.Web;

/// <summary>
/// Issues JWT access tokens containing Sekai user and tenant claims.
/// </summary>
/// <param name="jwtOptions">JWT signing, audience, issuer, and lifetime options.</param>
internal sealed class AuthTokenIssuer(IOptions<SekaiJwtOptions> jwtOptions)
{
    /// <summary>
    /// Creates a signed access token for a user and optional selected tenant.
    /// </summary>
    public IssuedToken Issue(long userId, long? tenantId)
    {
        var options = jwtOptions.Value;
        var expiresAt = DateTimeOffset.UtcNow.AddMinutes(options.AccessTokenLifetimeMinutes);
        var claims = new List<Claim>
        {
            new(SekaiAuthDefaults.UserIdClaimType, userId.ToString()),
            new(ClaimTypes.NameIdentifier, userId.ToString())
        };

        if (tenantId is not null)
        {
            claims.Add(new Claim(SekaiAuthDefaults.TenantIdClaimType, tenantId.Value.ToString()));
        }

        var credentials = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(options.SigningKey)),
            SecurityAlgorithms.HmacSha256);
        var jwt = new JwtSecurityToken(
            options.Issuer,
            options.Audience,
            claims,
            expires: expiresAt.UtcDateTime,
            signingCredentials: credentials);

        return new IssuedToken(new JwtSecurityTokenHandler().WriteToken(jwt), expiresAt);
    }
}
