using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using SekaiPlatform.Database;
using SekaiPlatform.Shared.Web;

internal sealed class AuthApplicationService(
    SekaiPlatformDbContext dbContext,
    AuthTokenIssuer tokenIssuer,
    ICurrentRequestContextAccessor contextAccessor)
{
    public async Task<IResult> LoginAsync(LoginRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Username) || string.IsNullOrWhiteSpace(request.Password))
        {
            return Unauthorized();
        }

        var user = await dbContext.Users
            .Include(item => item.UserTenants)
            .ThenInclude(item => item.Tenant)
            .SingleOrDefaultAsync(item => item.QqId == request.Username, cancellationToken);
        if (user is null || string.IsNullOrWhiteSpace(user.PasswordHash))
        {
            return Unauthorized();
        }

        var verification = new PasswordHasher<User>().VerifyHashedPassword(
            user,
            user.PasswordHash,
            request.Password);
        if (verification == PasswordVerificationResult.Failed)
        {
            return Unauthorized();
        }

        var tenants = GetActiveTenantResponses(user.UserTenants);
        if (tenants.Count == 0)
        {
            return Forbidden();
        }

        var currentTenant = tenants.Count == 1 ? tenants[0] : null;
        var token = tokenIssuer.Issue(user.Id, currentTenant?.Id);

        return Results.Json(CreateAuthResponse(user, currentTenant, tenants, token));
    }

    public async Task<IResult> GetSessionAsync(CancellationToken cancellationToken)
    {
        var context = contextAccessor.GetCurrent();
        if (context.UserId is null)
        {
            return Unauthorized();
        }

        var user = await dbContext.Users
            .Include(item => item.UserTenants)
            .ThenInclude(item => item.Tenant)
            .SingleOrDefaultAsync(item => item.Id == context.UserId.Value, cancellationToken);
        if (user is null)
        {
            return Unauthorized();
        }

        var tenants = GetActiveTenantResponses(user.UserTenants);
        var currentTenant = context.TenantId is null
            ? null
            : tenants.SingleOrDefault(item => item.Id == context.TenantId.Value);

        return Results.Json(new SessionResponse(
            CreateUserProfile(user),
            currentTenant,
            tenants));
    }

    public async Task<IResult> GetTenantsAsync(CancellationToken cancellationToken)
    {
        var context = contextAccessor.GetCurrent();
        if (context.UserId is null)
        {
            return Unauthorized();
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
    }

    public async Task<IResult> SwitchTenantAsync(SwitchTenantRequest request, CancellationToken cancellationToken)
    {
        var context = contextAccessor.GetCurrent();
        if (context.UserId is null)
        {
            return Unauthorized();
        }

        var user = await dbContext.Users
            .Include(item => item.UserTenants)
            .ThenInclude(item => item.Tenant)
            .SingleOrDefaultAsync(item => item.Id == context.UserId.Value, cancellationToken);
        if (user is null)
        {
            return Unauthorized();
        }

        var tenants = GetActiveTenantResponses(user.UserTenants);
        var currentTenant = tenants.SingleOrDefault(item => item.Id == request.TenantId);
        if (currentTenant is null)
        {
            return Forbidden();
        }

        var token = tokenIssuer.Issue(user.Id, currentTenant.Id);
        return Results.Json(CreateAuthResponse(user, currentTenant, tenants, token));
    }

    public async Task<IResult> InviteUserAsync(InvitationRequest request, CancellationToken cancellationToken)
    {
        var context = contextAccessor.GetCurrent();
        if (context.UserId is null || context.TenantId is null)
        {
            return Forbidden();
        }

        if (string.IsNullOrWhiteSpace(request.QqId) || string.IsNullOrWhiteSpace(request.Role))
        {
            return BadRequest();
        }

        var role = request.Role.Trim();
        var qqId = request.QqId.Trim();
        if (qqId.Length > 32 || !UserTenantRoles.All.Contains(role))
        {
            return BadRequest();
        }

        var inviter = await dbContext.UserTenants
            .SingleOrDefaultAsync(item =>
                item.TenantId == context.TenantId.Value
                && item.UserId == context.UserId.Value
                && item.Status == UserTenantStatuses.Active,
                cancellationToken);
        if (inviter is null || inviter.Role == UserTenantRoles.Normal)
        {
            return Forbidden();
        }

        if (role != UserTenantRoles.Normal && inviter.Role != UserTenantRoles.SuperAdmin)
        {
            return Forbidden();
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
    }

    private IResult Unauthorized()
    {
        return Error(StatusCodes.Status401Unauthorized, "Unauthorized.");
    }

    private IResult Forbidden()
    {
        return Error(StatusCodes.Status403Forbidden, "Forbidden.");
    }

    private IResult BadRequest()
    {
        return Error(StatusCodes.Status400BadRequest, "Invalid invitation request.");
    }

    private IResult Error(int statusCode, string message)
    {
        return AuthEndpointResults.Error(contextAccessor, statusCode, message);
    }

    private static AuthResponse CreateAuthResponse(
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

    private static UserProfileResponse CreateUserProfile(User user)
    {
        return new UserProfileResponse(user.Id, user.QqId, user.DisplayName, user.AvatarUrl);
    }

    private static List<TenantMembershipResponse> GetActiveTenantResponses(IEnumerable<UserTenant> memberships)
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

    private static string CreateDefaultPassword(string qqId)
    {
        return qqId.Length <= 6 ? qqId : qqId[^6..];
    }
}
