using SekaiPlatform.Shared.Web.Auth;

/// <summary>
/// Maps Auth Service internal endpoints for authentication and tenant membership operations.
/// </summary>
internal static class AuthEndpoints
{
    /// <summary>
    /// Registers internal authentication, session, tenant, and invitation endpoints.
    /// </summary>
    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/internal/auth/login", (
            LoginRequest request,
            AuthApplicationService authService,
            CancellationToken cancellationToken) =>
        {
            return authService.LoginAsync(request, cancellationToken);
        })
        .RequireInternalAuthorization(
            SekaiInternalAuthDefaults.AuthLoginScope,
            [SekaiInternalAuthDefaults.ApiServiceActor]);

        app.MapGet("/internal/auth/session", (
            AuthApplicationService authService,
            CancellationToken cancellationToken) =>
        {
            return authService.GetSessionAsync(cancellationToken);
        }).RequireInternalAuthorization(
            SekaiInternalAuthDefaults.AuthSessionReadScope,
            [SekaiInternalAuthDefaults.ApiServiceActor],
            requireSubject: true);

        app.MapGet("/internal/auth/tenants", (
            AuthApplicationService authService,
            CancellationToken cancellationToken) =>
        {
            return authService.GetTenantsAsync(cancellationToken);
        }).RequireInternalAuthorization(
            SekaiInternalAuthDefaults.AuthTenantsReadScope,
            [SekaiInternalAuthDefaults.ApiServiceActor],
            requireSubject: true);

        app.MapPut("/internal/auth/current-tenant", (
            SwitchTenantRequest request,
            AuthApplicationService authService,
            CancellationToken cancellationToken) =>
        {
            return authService.SwitchTenantAsync(request, cancellationToken);
        }).RequireInternalAuthorization(
            SekaiInternalAuthDefaults.AuthTenantSwitchScope,
            [SekaiInternalAuthDefaults.ApiServiceActor],
            requireSubject: true);

        app.MapPost("/internal/users/invitations", (
            InvitationRequest request,
            AuthApplicationService authService,
            CancellationToken cancellationToken) =>
        {
            return authService.InviteUserAsync(request, cancellationToken);
        }).RequireInternalAuthorization(
            SekaiInternalAuthDefaults.UsersInvitationsWriteScope,
            [SekaiInternalAuthDefaults.ApiServiceActor],
            requireSubject: true,
            requireTenant: true);

        return app;
    }
}
