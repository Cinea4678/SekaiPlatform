using SekaiPlatform.Shared.Web;

internal static class AuthEndpoints
{
    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/internal/auth/login", (
            LoginRequest request,
            AuthApplicationService authService,
            CancellationToken cancellationToken) =>
        {
            return authService.LoginAsync(request, cancellationToken);
        }).RequireRateLimiting("auth-login");

        app.MapGet("/internal/auth/session", (
            AuthApplicationService authService,
            CancellationToken cancellationToken) =>
        {
            return authService.GetSessionAsync(cancellationToken);
        }).RequireAuthorization(SekaiAuthorizationPolicies.LoggedIn);

        app.MapGet("/internal/auth/tenants", (
            AuthApplicationService authService,
            CancellationToken cancellationToken) =>
        {
            return authService.GetTenantsAsync(cancellationToken);
        }).RequireAuthorization(SekaiAuthorizationPolicies.LoggedIn);

        app.MapPut("/internal/auth/current-tenant", (
            SwitchTenantRequest request,
            AuthApplicationService authService,
            CancellationToken cancellationToken) =>
        {
            return authService.SwitchTenantAsync(request, cancellationToken);
        }).RequireAuthorization(SekaiAuthorizationPolicies.LoggedIn);

        app.MapPost("/internal/users/invitations", (
            InvitationRequest request,
            AuthApplicationService authService,
            CancellationToken cancellationToken) =>
        {
            return authService.InviteUserAsync(request, cancellationToken);
        }).RequireAuthorization(SekaiAuthorizationPolicies.TenantSelected);

        return app;
    }
}
