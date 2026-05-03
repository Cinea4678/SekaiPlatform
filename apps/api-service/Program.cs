using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.RateLimiting;
using SekaiPlatform.Database;
using SekaiPlatform.Shared.Web;

var builder = WebApplication.CreateBuilder(args);

builder.AddSekaiPlatformWebDefaults();
builder.Services.AddSekaiPlatformDatabase(builder.Configuration);
builder.Services.AddSekaiPlatformInternalHttpClient("auth-service", builder.Configuration, "AuthService");
builder.Services.AddSekaiPlatformInternalHttpClient("asset-service", builder.Configuration, "AssetService");
builder.Services.AddSekaiPlatformInternalHttpClient("search-service", builder.Configuration, "SearchService");
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddFixedWindowLimiter("auth-login", limiter =>
    {
        limiter.PermitLimit = 30;
        limiter.QueueLimit = 0;
        limiter.Window = TimeSpan.FromMinutes(1);
    });
});

var app = builder.Build();

app.Use(async (httpContext, next) =>
{
    httpContext.Request.Headers.Remove(SekaiHeaders.UserId);
    httpContext.Request.Headers.Remove(SekaiHeaders.TenantId);
    await next();
});

app.UseSekaiPlatformWebDefaults();
app.UseRateLimiter();
app.MapHealthChecks("/health");

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

app.MapGet("/api/internal-services/health", async (
    IHttpClientFactory httpClientFactory,
    CancellationToken cancellationToken) =>
{
    var checks = await Task.WhenAll(
        CheckHealthAsync(httpClientFactory, "auth-service", cancellationToken),
        CheckHealthAsync(httpClientFactory, "asset-service", cancellationToken),
        CheckHealthAsync(httpClientFactory, "search-service", cancellationToken));

    var isHealthy = checks.All(check => check.Healthy);
    var response = new InternalServicesHealthResponse(
        isHealthy ? "healthy" : "unhealthy",
        checks);

    return Results.Json(
        response,
        statusCode: isHealthy ? StatusCodes.Status200OK : StatusCodes.Status503ServiceUnavailable);
});

await DatabaseInitializer.InitializeAsync(app);

app.Run();

static async Task<HttpResponseMessage> SendAuthServiceAsync(
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

static async Task<IResult> ForwardAuthResponseAsync(
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

static void SetAuthenticationCookie(HttpContext httpContext, AuthTokenResponse token)
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

static bool ShouldUseSecureCookie(HttpContext httpContext)
{
    var environment = httpContext.RequestServices.GetRequiredService<IHostEnvironment>();
    return httpContext.Request.IsHttps || !environment.IsDevelopment();
}

static void ClearAuthenticationCookie(HttpResponse response)
{
    response.Cookies.Delete(
        SekaiAuthDefaults.AuthenticationCookieName,
        new CookieOptions
        {
            HttpOnly = true,
            SameSite = SameSiteMode.Lax
        });
}

static async Task<InternalServiceHealthItem> CheckHealthAsync(
    IHttpClientFactory httpClientFactory,
    string serviceName,
    CancellationToken cancellationToken)
{
    try
    {
        using var response = await httpClientFactory
            .CreateClient(serviceName)
            .GetAsync("/health", cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);

        return new InternalServiceHealthItem(
            serviceName,
            response.IsSuccessStatusCode,
            (int)response.StatusCode,
            response.IsSuccessStatusCode ? "healthy" : "unhealthy",
            body,
            null);
    }
    catch (Exception exception)
    {
        return new InternalServiceHealthItem(
            serviceName,
            false,
            null,
            "unhealthy",
            null,
            exception.Message);
    }
}

internal sealed record InternalServicesHealthResponse(
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("services")] IReadOnlyCollection<InternalServiceHealthItem> Services);

internal sealed record InternalServiceHealthItem(
    [property: JsonPropertyName("service")] string Service,
    [property: JsonPropertyName("healthy")] bool Healthy,
    [property: JsonPropertyName("status_code")] int? StatusCode,
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("body")] string? Body,
    [property: JsonPropertyName("error")] string? Error);

internal sealed record LoginRequest(
    [property: JsonPropertyName("username")] string Username,
    [property: JsonPropertyName("password")] string Password);

internal sealed record SwitchTenantRequest(
    [property: JsonPropertyName("tenant_id")] long TenantId);

internal sealed record InvitationRequest(
    [property: JsonPropertyName("qq_id")] string QqId,
    [property: JsonPropertyName("role")] string Role);

internal sealed record LogoutResponse(
    [property: JsonPropertyName("ok")] bool Ok);

internal sealed record AuthTokenResponse(
    [property: JsonPropertyName("access_token")] string AccessToken,
    [property: JsonPropertyName("expires_at")] DateTimeOffset ExpiresAt);

public partial class Program;
