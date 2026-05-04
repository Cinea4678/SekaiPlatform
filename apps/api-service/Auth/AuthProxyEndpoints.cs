using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using SekaiPlatform.Shared.Web;

/// <summary>
/// Maps API Service authentication endpoints that proxy frontend calls to Auth Service.
/// </summary>
internal static class AuthProxyEndpoints
{
    /// <summary>
    /// Registers public authentication, session, tenant, and invitation proxy endpoints.
    /// </summary>
    public static IEndpointRouteBuilder MapAuthProxyEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/auth/login", async Task<IResult> (
            LoginRequest request,
            IHttpClientFactory httpClientFactory,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            using var response = await SendAuthServiceAsync(
                httpClientFactory,
                httpContext,
                HttpMethod.Post,
                "/internal/auth/login",
                request,
                cancellationToken);

            return await ForwardAuthResponseAsync(response, httpContext, setAuthenticationCookie: true, cancellationToken);
        }).RequireRateLimiting("auth-login");

        app.MapPost("/api/auth/logout", (HttpContext httpContext) =>
        {
            ClearAuthenticationCookie(httpContext.Response);
            return Results.Json(new LogoutResponse(true));
        });

        app.MapGet("/api/auth/session", async Task<IResult> (
            IHttpClientFactory httpClientFactory,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            using var response = await SendAuthServiceAsync(
                httpClientFactory,
                httpContext,
                HttpMethod.Get,
                "/internal/auth/session",
                body: null,
                cancellationToken);

            return await ForwardAuthResponseAsync(response, httpContext, setAuthenticationCookie: false, cancellationToken);
        }).RequireAuthorization(SekaiAuthorizationPolicies.LoggedIn);

        app.MapGet("/api/auth/tenants", async Task<IResult> (
            IHttpClientFactory httpClientFactory,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            using var response = await SendAuthServiceAsync(
                httpClientFactory,
                httpContext,
                HttpMethod.Get,
                "/internal/auth/tenants",
                body: null,
                cancellationToken);

            return await ForwardAuthResponseAsync(response, httpContext, setAuthenticationCookie: false, cancellationToken);
        }).RequireAuthorization(SekaiAuthorizationPolicies.LoggedIn);

        app.MapPut("/api/auth/current-tenant", async Task<IResult> (
            SwitchTenantRequest request,
            IHttpClientFactory httpClientFactory,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            using var response = await SendAuthServiceAsync(
                httpClientFactory,
                httpContext,
                HttpMethod.Put,
                "/internal/auth/current-tenant",
                request,
                cancellationToken);

            return await ForwardAuthResponseAsync(response, httpContext, setAuthenticationCookie: true, cancellationToken);
        }).RequireAuthorization(SekaiAuthorizationPolicies.LoggedIn);

        app.MapPost("/api/users/invitations", async Task<IResult> (
            InvitationRequest request,
            IHttpClientFactory httpClientFactory,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            using var response = await SendAuthServiceAsync(
                httpClientFactory,
                httpContext,
                HttpMethod.Post,
                "/internal/users/invitations",
                request,
                cancellationToken);

            return await ForwardAuthResponseAsync(response, httpContext, setAuthenticationCookie: false, cancellationToken);
        }).RequireAuthorization(SekaiAuthorizationPolicies.TenantSelected);

        return app;
    }

    /// <summary>
    /// Sends a request to Auth Service while preserving bearer authentication from headers or cookies.
    /// </summary>
    private static async Task<HttpResponseMessage> SendAuthServiceAsync(
        IHttpClientFactory httpClientFactory,
        HttpContext httpContext,
        HttpMethod method,
        string path,
        object? body,
        CancellationToken cancellationToken)
    {
        var request = new HttpRequestMessage(method, path);
        if (body is not null)
        {
            request.Content = JsonContent.Create(body);
        }

        if (httpContext.Request.Headers.TryGetValue("Authorization", out var authorization)
            && AuthenticationHeaderValue.TryParse(authorization.ToString(), out var header))
        {
            request.Headers.Authorization = header;
        }
        else if (httpContext.Request.Cookies.TryGetValue(SekaiAuthDefaults.AuthenticationCookieName, out var token)
            && !string.IsNullOrWhiteSpace(token))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        return await httpClientFactory
            .CreateClient("auth-service")
            .SendAsync(request, cancellationToken);
    }

    /// <summary>
    /// Forwards an Auth Service response and optionally stores a returned access token in a cookie.
    /// </summary>
    private static async Task<IResult> ForwardAuthResponseAsync(
        HttpResponseMessage response,
        HttpContext httpContext,
        bool setAuthenticationCookie,
        CancellationToken cancellationToken)
    {
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (setAuthenticationCookie && response.IsSuccessStatusCode)
        {
            var token = JsonSerializer.Deserialize<AuthTokenResponse>(body);
            if (token is not null)
            {
                SetAuthenticationCookie(httpContext, token);
            }
        }

        var contentType = response.Content.Headers.ContentType?.ToString() ?? "application/json";
        return Results.Content(body, contentType, statusCode: (int)response.StatusCode);
    }

    /// <summary>
    /// Writes the frontend authentication cookie using the issued access token.
    /// </summary>
    private static void SetAuthenticationCookie(HttpContext httpContext, AuthTokenResponse token)
    {
        httpContext.Response.Cookies.Append(
            SekaiAuthDefaults.AuthenticationCookieName,
            token.AccessToken,
            new CookieOptions
            {
                HttpOnly = true,
                Secure = ShouldUseSecureCookie(httpContext),
                SameSite = SameSiteMode.Lax,
                Expires = token.ExpiresAt
            });
    }

    /// <summary>
    /// Decides whether the auth cookie must be marked secure for the current environment.
    /// </summary>
    private static bool ShouldUseSecureCookie(HttpContext httpContext)
    {
        var environment = httpContext.RequestServices.GetRequiredService<IHostEnvironment>();
        return httpContext.Request.IsHttps || !environment.IsDevelopment();
    }

    /// <summary>
    /// Deletes the frontend authentication cookie on logout.
    /// </summary>
    private static void ClearAuthenticationCookie(HttpResponse response)
    {
        response.Cookies.Delete(
            SekaiAuthDefaults.AuthenticationCookieName,
            new CookieOptions
            {
                HttpOnly = true,
                SameSite = SameSiteMode.Lax
            });
    }
}
