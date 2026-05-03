using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using SekaiPlatform.Database;
using SekaiPlatform.Shared.Web;

var builder = WebApplication.CreateBuilder(args);

builder.AddSekaiPlatformWebDefaults();
builder.Services.AddSekaiPlatformDatabase(builder.Configuration);
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddFixedWindowLimiter("auth-login", limiter =>
    {
        limiter.PermitLimit = 60;
        limiter.QueueLimit = 0;
        limiter.Window = TimeSpan.FromMinutes(1);
    });
});

var app = builder.Build();

app.UseSekaiPlatformWebDefaults();
app.UseRateLimiter();
app.MapHealthChecks("/health");

app.MapPost("/internal/auth/login", async Task<IResult> (
    LoginRequest request,
    SekaiPlatformDbContext dbContext,
    IOptions<SekaiJwtOptions> jwtOptions,
    ICurrentRequestContextAccessor contextAccessor,
    CancellationToken cancellationToken) =>
{
    if (string.IsNullOrWhiteSpace(request.Username) || string.IsNullOrWhiteSpace(request.Password))
    {
        return Error(contextAccessor, StatusCodes.Status401Unauthorized, "Unauthorized.");
    }

    var user = await dbContext.Users
        .Include(item => item.UserTenants)
        .ThenInclude(item => item.Tenant)
        .SingleOrDefaultAsync(item => item.QqId == request.Username, cancellationToken);
    if (user is null || string.IsNullOrWhiteSpace(user.PasswordHash))
    {
        return Error(contextAccessor, StatusCodes.Status401Unauthorized, "Unauthorized.");
    }

    var verification = new PasswordHasher<User>().VerifyHashedPassword(
        user,
        user.PasswordHash,
        request.Password);
    if (verification == PasswordVerificationResult.Failed)
    {
        return Error(contextAccessor, StatusCodes.Status401Unauthorized, "Unauthorized.");
    }

    var tenants = GetActiveTenantResponses(user.UserTenants);
    if (tenants.Count == 0)
    {
        return Error(contextAccessor, StatusCodes.Status403Forbidden, "Forbidden.");
    }

    var currentTenant = tenants.Count == 1 ? tenants[0] : null;
    var token = IssueToken(jwtOptions.Value, user.Id, currentTenant?.Id);

    return Results.Json(CreateAuthResponse(user, currentTenant, tenants, token));
}).RequireRateLimiting("auth-login");

app.MapGet("/internal/auth/session", async Task<IResult> (
    ICurrentRequestContextAccessor contextAccessor,
    SekaiPlatformDbContext dbContext,
    CancellationToken cancellationToken) =>
{
    var context = contextAccessor.GetCurrent();
    if (context.UserId is null)
    {
        return Error(contextAccessor, StatusCodes.Status401Unauthorized, "Unauthorized.");
    }

    var user = await dbContext.Users
        .Include(item => item.UserTenants)
        .ThenInclude(item => item.Tenant)
        .SingleOrDefaultAsync(item => item.Id == context.UserId.Value, cancellationToken);
    if (user is null)
    {
        return Error(contextAccessor, StatusCodes.Status401Unauthorized, "Unauthorized.");
    }

    var tenants = GetActiveTenantResponses(user.UserTenants);
    var currentTenant = context.TenantId is null
        ? null
        : tenants.SingleOrDefault(item => item.Id == context.TenantId.Value);

    return Results.Json(new SessionResponse(
        CreateUserProfile(user),
        currentTenant,
        tenants));
}).RequireAuthorization(SekaiAuthorizationPolicies.LoggedIn);

app.MapGet("/internal/auth/tenants", async Task<IResult> (
    ICurrentRequestContextAccessor contextAccessor,
    SekaiPlatformDbContext dbContext,
    CancellationToken cancellationToken) =>
{
    var context = contextAccessor.GetCurrent();
    if (context.UserId is null)
    {
        return Error(contextAccessor, StatusCodes.Status401Unauthorized, "Unauthorized.");
    }

    var tenants = await dbContext.UserTenants
        .Where(item => item.UserId == context.UserId.Value && item.Status == UserTenantStatuses.Active)
        .OrderBy(item => item.Tenant!.Id)
        .Select(item => new TenantMembershipResponse(
            item.TenantId,
            item.Tenant!.Name,
            item.Tenant.AvatarUrl,
            item.Role))
        .ToListAsync(cancellationToken);

    return Results.Json(tenants);
}).RequireAuthorization(SekaiAuthorizationPolicies.LoggedIn);

app.MapPut("/internal/auth/current-tenant", async Task<IResult> (
    SwitchTenantRequest request,
    ICurrentRequestContextAccessor contextAccessor,
    SekaiPlatformDbContext dbContext,
    IOptions<SekaiJwtOptions> jwtOptions,
    CancellationToken cancellationToken) =>
{
    var context = contextAccessor.GetCurrent();
    if (context.UserId is null)
    {
        return Error(contextAccessor, StatusCodes.Status401Unauthorized, "Unauthorized.");
    }

    var user = await dbContext.Users
        .Include(item => item.UserTenants)
        .ThenInclude(item => item.Tenant)
        .SingleOrDefaultAsync(item => item.Id == context.UserId.Value, cancellationToken);
    if (user is null)
    {
        return Error(contextAccessor, StatusCodes.Status401Unauthorized, "Unauthorized.");
    }

    var tenants = GetActiveTenantResponses(user.UserTenants);
    var currentTenant = tenants.SingleOrDefault(item => item.Id == request.TenantId);
    if (currentTenant is null)
    {
        return Error(contextAccessor, StatusCodes.Status403Forbidden, "Forbidden.");
    }

    var token = IssueToken(jwtOptions.Value, user.Id, currentTenant.Id);
    return Results.Json(CreateAuthResponse(user, currentTenant, tenants, token));
}).RequireAuthorization(SekaiAuthorizationPolicies.LoggedIn);

app.MapPost("/internal/users/invitations", async Task<IResult> (
    InvitationRequest request,
    ICurrentRequestContextAccessor contextAccessor,
    SekaiPlatformDbContext dbContext,
    CancellationToken cancellationToken) =>
{
    var context = contextAccessor.GetCurrent();
    if (context.UserId is null || context.TenantId is null)
    {
        return Error(contextAccessor, StatusCodes.Status403Forbidden, "Forbidden.");
    }

    if (string.IsNullOrWhiteSpace(request.QqId) || string.IsNullOrWhiteSpace(request.Role))
    {
        return Error(contextAccessor, StatusCodes.Status400BadRequest, "Invalid invitation request.");
    }

    var role = request.Role.Trim();
    var qqId = request.QqId.Trim();
    if (qqId.Length > 32 || !UserTenantRoles.All.Contains(role))
    {
        return Error(contextAccessor, StatusCodes.Status400BadRequest, "Invalid invitation request.");
    }

    var inviter = await dbContext.UserTenants
        .SingleOrDefaultAsync(item =>
            item.TenantId == context.TenantId.Value
            && item.UserId == context.UserId.Value
            && item.Status == UserTenantStatuses.Active,
            cancellationToken);
    if (inviter is null || inviter.Role == UserTenantRoles.Normal)
    {
        return Error(contextAccessor, StatusCodes.Status403Forbidden, "Forbidden.");
    }

    if (role != UserTenantRoles.Normal && inviter.Role != UserTenantRoles.SuperAdmin)
    {
        return Error(contextAccessor, StatusCodes.Status403Forbidden, "Forbidden.");
    }

    var now = DateTimeOffset.UtcNow;
    var user = await dbContext.Users.SingleOrDefaultAsync(item => item.QqId == qqId, cancellationToken);
    var createdUser = user is null;
    string? defaultPassword = null;
    if (user is null)
    {
        defaultPassword = CreateDefaultPassword(qqId);
        user = new User
        {
            QqId = qqId,
            DisplayName = qqId,
            CreatedAt = now,
            UpdatedAt = now
        };
        user.PasswordHash = new PasswordHasher<User>().HashPassword(user, defaultPassword);
        dbContext.Users.Add(user);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    var membership = await dbContext.UserTenants.FindAsync(
        new object[] { context.TenantId.Value, user.Id },
        cancellationToken);
    var createdMembership = membership is null;
    if (membership is null)
    {
        membership = new UserTenant
        {
            TenantId = context.TenantId.Value,
            UserId = user.Id,
            CreatedAt = now
        };
        dbContext.UserTenants.Add(membership);
    }
    else if (membership.Role != role)
    {
        return Error(
            contextAccessor,
            StatusCodes.Status409Conflict,
            "User already belongs to current tenant with another role.");
    }

    membership.Role = role;
    membership.Status = UserTenantStatuses.Active;
    membership.DeletedAt = null;
    membership.UpdatedAt = now;

    await dbContext.SaveChangesAsync(cancellationToken);

    return Results.Json(new InvitationResponse(
        CreateUserProfile(user),
        new InvitationMembershipResponse(membership.TenantId, membership.UserId, membership.Role, membership.Status),
        createdUser,
        createdMembership,
        defaultPassword));
}).RequireAuthorization(SekaiAuthorizationPolicies.TenantSelected);

app.Run();

static IResult Error(ICurrentRequestContextAccessor contextAccessor, int statusCode, string message)
{
    var requestContext = contextAccessor.GetCurrent();
    return Results.Json(new ErrorResponse(message, requestContext.TraceId), statusCode: statusCode);
}

static AuthResponse CreateAuthResponse(
    User user,
    TenantMembershipResponse? currentTenant,
    IReadOnlyCollection<TenantMembershipResponse> tenants,
    IssuedToken token)
{
    return new AuthResponse(
        token.AccessToken,
        token.ExpiresAt,
        CreateUserProfile(user),
        currentTenant,
        tenants);
}

static UserProfileResponse CreateUserProfile(User user)
{
    return new UserProfileResponse(user.Id, user.QqId, user.DisplayName, user.AvatarUrl);
}

static List<TenantMembershipResponse> GetActiveTenantResponses(IEnumerable<UserTenant> memberships)
{
    return memberships
        .Where(item => item.Status == UserTenantStatuses.Active && item.Tenant is not null)
        .OrderBy(item => item.TenantId)
        .Select(item => new TenantMembershipResponse(
            item.TenantId,
            item.Tenant!.Name,
            item.Tenant.AvatarUrl,
            item.Role))
        .ToList();
}

static IssuedToken IssueToken(SekaiJwtOptions options, long userId, long? tenantId)
{
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

static string CreateDefaultPassword(string qqId)
{
    return qqId.Length <= 6 ? qqId : qqId[^6..];
}

internal sealed record IssuedToken(
    string AccessToken,
    DateTimeOffset ExpiresAt);

internal sealed record LoginRequest(
    [property: JsonPropertyName("username")] string Username,
    [property: JsonPropertyName("password")] string Password);

internal sealed record SwitchTenantRequest(
    [property: JsonPropertyName("tenant_id")] long TenantId);

internal sealed record InvitationRequest(
    [property: JsonPropertyName("qq_id")] string? QqId,
    [property: JsonPropertyName("role")] string? Role);

internal sealed record AuthResponse(
    [property: JsonPropertyName("access_token")] string AccessToken,
    [property: JsonPropertyName("expires_at")] DateTimeOffset ExpiresAt,
    [property: JsonPropertyName("user")] UserProfileResponse User,
    [property: JsonPropertyName("current_tenant")] TenantMembershipResponse? CurrentTenant,
    [property: JsonPropertyName("tenants")] IReadOnlyCollection<TenantMembershipResponse> Tenants);

internal sealed record SessionResponse(
    [property: JsonPropertyName("user")] UserProfileResponse User,
    [property: JsonPropertyName("current_tenant")] TenantMembershipResponse? CurrentTenant,
    [property: JsonPropertyName("tenants")] IReadOnlyCollection<TenantMembershipResponse> Tenants);

internal sealed record UserProfileResponse(
    [property: JsonPropertyName("id")] long Id,
    [property: JsonPropertyName("qq_id")] string? QqId,
    [property: JsonPropertyName("display_name")] string? DisplayName,
    [property: JsonPropertyName("avatar_url")] string? AvatarUrl);

internal sealed record TenantMembershipResponse(
    [property: JsonPropertyName("id")] long Id,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("avatar_url")] string? AvatarUrl,
    [property: JsonPropertyName("role")] string Role);

internal sealed record InvitationMembershipResponse(
    [property: JsonPropertyName("tenant_id")] long TenantId,
    [property: JsonPropertyName("user_id")] long UserId,
    [property: JsonPropertyName("role")] string Role,
    [property: JsonPropertyName("status")] string Status);

internal sealed record InvitationResponse(
    [property: JsonPropertyName("user")] UserProfileResponse User,
    [property: JsonPropertyName("membership")] InvitationMembershipResponse Membership,
    [property: JsonPropertyName("created_user")] bool CreatedUser,
    [property: JsonPropertyName("created_membership")] bool CreatedMembership,
    [property: JsonPropertyName("default_password")] string? DefaultPassword);

public partial class Program;
