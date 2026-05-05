extern alias AuthService;

using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SekaiPlatform.Database;
using SekaiPlatform.Shared.Web.Auth;
using AuthServiceProgram = AuthService::Program;

namespace SekaiPlatform.IntegrationTests;

/// <summary>
/// Exercises authentication, tenant selection, and invitation flows through API and Auth services.
/// </summary>
[Collection(IntegrationTestCollection.Name)]
public sealed class AuthApiTests : IDisposable
{
    private readonly IntegrationTestDatabaseFixture fixture;
    private readonly AuthServiceFactory authFactory;
    private readonly ApiServiceFactory apiFactory;

    /// <summary>
    /// Creates API and Auth service hosts backed by the shared integration database.
    /// </summary>
    public AuthApiTests(IntegrationTestDatabaseFixture fixture)
    {
        this.fixture = fixture;
        authFactory = new AuthServiceFactory(fixture.ConnectionString);
        apiFactory = new ApiServiceFactory(fixture.ConnectionString, authFactory);
    }

    /// <summary>
    /// Verifies login returns both cookie and bearer credentials that can read the session.
    /// </summary>
    [Fact]
    public async Task Login_ReturnsTokenCookieAndSessionWorksWithBearerAndCookie()
    {
        using var client = apiFactory.CreateClient();

        using var login = await client.PostAsJsonAsync("/api/auth/login", new
        {
            username = IntegrationTestDatabaseFixture.AdminQqId,
            password = IntegrationTestDatabaseFixture.AdminPassword
        });

        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        Assert.True(login.Headers.TryGetValues("Set-Cookie", out var cookies));
        Assert.Contains(cookies, item => item.Contains("SEKAI_PLATFORM_AUTH=", StringComparison.Ordinal));

        var json = await ReadJsonAsync(login);
        var token = json.RootElement.GetProperty("access_token").GetString();
        Assert.False(string.IsNullOrWhiteSpace(token));
        Assert.Equal(
            IntegrationTestDatabaseFixture.TenantName,
            json.RootElement.GetProperty("current_tenant").GetProperty("name").GetString());

        using var bearerSession = await SendWithBearerAsync(client, HttpMethod.Get, "/api/auth/session", token!);
        Assert.Equal(HttpStatusCode.OK, bearerSession.StatusCode);

        var cookieValue = cookies!.First(item => item.StartsWith("SEKAI_PLATFORM_AUTH=", StringComparison.Ordinal))
            .Split(';', 2)[0];
        using var cookieSessionRequest = new HttpRequestMessage(HttpMethod.Get, "/api/auth/session");
        cookieSessionRequest.Headers.Add("Cookie", cookieValue);
        using var cookieSession = await client.SendAsync(cookieSessionRequest);
        Assert.Equal(HttpStatusCode.OK, cookieSession.StatusCode);
    }

    /// <summary>
    /// Ensures invalid credentials return the platform error envelope with Unauthorized.
    /// </summary>
    [Fact]
    public async Task Login_WithWrongPassword_ReturnsUnauthorized()
    {
        using var client = apiFactory.CreateClient();

        using var response = await client.PostAsJsonAsync("/api/auth/login", new
        {
            username = IntegrationTestDatabaseFixture.AdminQqId,
            password = "wrong-password"
        });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        await AssertErrorResponseAsync(response);
    }

    /// <summary>
    /// Verifies a multi-tenant user must choose a current tenant before tenant-scoped calls.
    /// </summary>
    [Fact]
    public async Task MultiTenantUser_MustSelectTenantBeforeTenantScopedApi()
    {
        using var client = apiFactory.CreateClient();
        var login = await LoginAsync(
            client,
            IntegrationTestDatabaseFixture.MultiTenantUserQqId,
            IntegrationTestDatabaseFixture.MultiTenantUserPassword);

        Assert.Equal(JsonValueKind.Null, login.Json.RootElement.GetProperty("current_tenant").ValueKind);
        Assert.Equal(2, login.Json.RootElement.GetProperty("tenants").GetArrayLength());

        using var forbidden = await SendWithBearerAsync(
            client,
            HttpMethod.Post,
            "/api/users/invitations",
            login.Token,
            new { qq_id = CreateUniqueQqId(), role = UserTenantRoles.Normal },
            configure: request =>
            {
                request.Headers.Add("X-Sekai-User-Id", "1");
                request.Headers.Add("X-Sekai-Tenant-Id", "1");
            });
        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);

        var tenantId = login.Json.RootElement
            .GetProperty("tenants")[0]
            .GetProperty("id")
            .GetInt64();
        using var switched = await SendWithBearerAsync(
            client,
            HttpMethod.Put,
            "/api/auth/current-tenant",
            login.Token,
            new { tenant_id = tenantId });

        Assert.Equal(HttpStatusCode.OK, switched.StatusCode);
        var switchedJson = await ReadJsonAsync(switched);
        Assert.Equal(tenantId, switchedJson.RootElement.GetProperty("current_tenant").GetProperty("id").GetInt64());
        Assert.False(string.IsNullOrWhiteSpace(switchedJson.RootElement.GetProperty("access_token").GetString()));
    }

    /// <summary>
    /// Ensures externally supplied service-context headers cannot authenticate a request.
    /// </summary>
    [Fact]
    public async Task SpoofedContextHeaders_DoNotBypassAuthentication()
    {
        using var client = apiFactory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/users/invitations")
        {
            Content = JsonContent.Create(new { qq_id = CreateUniqueQqId(), role = UserTenantRoles.Normal })
        };
        request.Headers.Add("X-Sekai-User-Id", "1");
        request.Headers.Add("X-Sekai-Tenant-Id", "1");

        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        await AssertErrorResponseAsync(response);
    }

    /// <summary>
    /// Verifies super administrators can invite users and repeated invitations are idempotent.
    /// </summary>
    [Fact]
    public async Task SuperAdmin_CanInviteNewUserWithDefaultPasswordAndRepeatIsIdempotent()
    {
        using var client = apiFactory.CreateClient();
        var login = await LoginAsync(
            client,
            IntegrationTestDatabaseFixture.AdminQqId,
            IntegrationTestDatabaseFixture.AdminPassword);
        var qqId = CreateUniqueQqId();

        using var first = await SendWithBearerAsync(
            client,
            HttpMethod.Post,
            "/api/users/invitations",
            login.Token,
            new { qq_id = qqId, role = UserTenantRoles.Normal });
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        var firstJson = await ReadJsonAsync(first);
        Assert.True(firstJson.RootElement.GetProperty("created_user").GetBoolean());
        Assert.True(firstJson.RootElement.GetProperty("created_membership").GetBoolean());
        Assert.Equal(qqId[^6..], firstJson.RootElement.GetProperty("default_password").GetString());

        await using (var dbContext = fixture.CreateDbContext())
        {
            var user = await dbContext.Users.SingleAsync(item => item.QqId == qqId);
            Assert.NotNull(user.PasswordHash);
            var result = new PasswordHasher<User>().VerifyHashedPassword(user, user.PasswordHash!, qqId[^6..]);
            Assert.NotEqual(PasswordVerificationResult.Failed, result);
        }

        using var second = await SendWithBearerAsync(
            client,
            HttpMethod.Post,
            "/api/users/invitations",
            login.Token,
            new { qq_id = qqId, role = UserTenantRoles.Normal });
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        var secondJson = await ReadJsonAsync(second);
        Assert.False(secondJson.RootElement.GetProperty("created_user").GetBoolean());
        Assert.False(secondJson.RootElement.GetProperty("created_membership").GetBoolean());
        Assert.Equal(JsonValueKind.Null, secondJson.RootElement.GetProperty("default_password").ValueKind);
    }

    /// <summary>
    /// Ensures tenant admins and normal users cannot grant roles beyond their authority.
    /// </summary>
    [Fact]
    public async Task Invitation_RejectsInsufficientRoles()
    {
        using var client = apiFactory.CreateClient();
        var tenantAdmin = await LoginAsync(
            client,
            IntegrationTestDatabaseFixture.TenantAdminQqId,
            IntegrationTestDatabaseFixture.TenantAdminPassword);
        var normalUser = await LoginAsync(
            client,
            IntegrationTestDatabaseFixture.NormalUserQqId,
            IntegrationTestDatabaseFixture.NormalUserPassword);

        using var adminGrantingSuperAdmin = await SendWithBearerAsync(
            client,
            HttpMethod.Post,
            "/api/users/invitations",
            tenantAdmin.Token,
            new { qq_id = CreateUniqueQqId(), role = UserTenantRoles.SuperAdmin });
        Assert.Equal(HttpStatusCode.Forbidden, adminGrantingSuperAdmin.StatusCode);
        await AssertErrorResponseAsync(adminGrantingSuperAdmin);

        using var normalInvitingUser = await SendWithBearerAsync(
            client,
            HttpMethod.Post,
            "/api/users/invitations",
            normalUser.Token,
            new { qq_id = CreateUniqueQqId(), role = UserTenantRoles.Normal });
        Assert.Equal(HttpStatusCode.Forbidden, normalInvitingUser.StatusCode);
        await AssertErrorResponseAsync(normalInvitingUser);
    }

    /// <summary>
    /// Verifies inviting an existing member fails without downgrading their role.
    /// </summary>
    [Fact]
    public async Task Invitation_DoesNotChangeExistingMembershipRole()
    {
        using var client = apiFactory.CreateClient();
        var tenantAdmin = await LoginAsync(
            client,
            IntegrationTestDatabaseFixture.TenantAdminQqId,
            IntegrationTestDatabaseFixture.TenantAdminPassword);

        using var response = await SendWithBearerAsync(
            client,
            HttpMethod.Post,
            "/api/users/invitations",
            tenantAdmin.Token,
            new { qq_id = IntegrationTestDatabaseFixture.AdminQqId, role = UserTenantRoles.Normal });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        await AssertErrorResponseAsync(response);

        await using var dbContext = fixture.CreateDbContext();
        var role = await dbContext.UserTenants
            .Where(item => item.User!.QqId == IntegrationTestDatabaseFixture.AdminQqId)
            .Select(item => item.Role)
            .SingleAsync();
        Assert.Equal(UserTenantRoles.SuperAdmin, role);
    }

    /// <summary>
    /// Ensures invitation validation rejects missing QQ IDs and blank roles.
    /// </summary>
    [Fact]
    public async Task Invitation_WithNullOrBlankFields_ReturnsBadRequestError()
    {
        using var client = apiFactory.CreateClient();
        var login = await LoginAsync(
            client,
            IntegrationTestDatabaseFixture.AdminQqId,
            IntegrationTestDatabaseFixture.AdminPassword);

        using var nullQqId = await SendWithBearerAsync(
            client,
            HttpMethod.Post,
            "/api/users/invitations",
            login.Token,
            new { qq_id = (string?)null, role = UserTenantRoles.Normal });
        Assert.Equal(HttpStatusCode.BadRequest, nullQqId.StatusCode);
        await AssertErrorResponseAsync(nullQqId);

        using var blankRole = await SendWithBearerAsync(
            client,
            HttpMethod.Post,
            "/api/users/invitations",
            login.Token,
            new { qq_id = CreateUniqueQqId(), role = " " });
        Assert.Equal(HttpStatusCode.BadRequest, blankRole.StatusCode);
        await AssertErrorResponseAsync(blankRole);
    }

    /// <summary>
    /// Verifies logout clears the authentication cookie.
    /// </summary>
    [Fact]
    public async Task Logout_ClearsAuthenticationCookie()
    {
        using var client = apiFactory.CreateClient();

        using var response = await client.PostAsync("/api/auth/logout", content: null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(response.Headers.TryGetValues("Set-Cookie", out var cookies));
        Assert.Contains(cookies, item => item.Contains("SEKAI_PLATFORM_AUTH=", StringComparison.Ordinal));
    }

    /// <summary>
    /// Ensures Auth Service internal endpoints reject requests without an internal token.
    /// </summary>
    [Fact]
    public async Task InternalSession_WithoutInternalToken_ReturnsUnauthorized()
    {
        using var client = authFactory.CreateClient();

        using var response = await client.GetAsync("/internal/auth/session");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    /// <summary>
    /// Ensures user-proxy Auth Service endpoints require a delegated subject user.
    /// </summary>
    [Fact]
    public async Task InternalSession_WithoutSubjectClaim_ReturnsForbidden()
    {
        using var client = authFactory.CreateClient();
        var request = new HttpRequestMessage(HttpMethod.Get, "/internal/auth/session");
        request.Headers.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            IntegrationTestInternalAuth.Issue(
                SekaiInternalAuthDefaults.ApiServiceActor,
                SekaiInternalAuthDefaults.AuthServiceActor,
                SekaiInternalAuthDefaults.AuthSessionReadScope));

        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    /// <summary>
    /// Disposes in-memory service hosts created for the test case.
    /// </summary>
    public void Dispose()
    {
        apiFactory.Dispose();
        authFactory.Dispose();
    }

    /// <summary>
    /// Logs in through the API service and returns the bearer token plus raw JSON response.
    /// </summary>
    private static async Task<LoginResult> LoginAsync(HttpClient client, string username, string password)
    {
        using var response = await client.PostAsJsonAsync("/api/auth/login", new { username, password });
        response.EnsureSuccessStatusCode();

        var json = await ReadJsonAsync(response);
        var token = json.RootElement.GetProperty("access_token").GetString();
        Assert.False(string.IsNullOrWhiteSpace(token));

        return new LoginResult(token!, json);
    }

    /// <summary>
    /// Sends an API request with bearer authentication and optional JSON body.
    /// </summary>
    private static Task<HttpResponseMessage> SendWithBearerAsync(
        HttpClient client,
        HttpMethod method,
        string path,
        string token,
        object? body = null,
        Action<HttpRequestMessage>? configure = null)
    {
        var request = new HttpRequestMessage(method, path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        if (body is not null)
        {
            request.Content = JsonContent.Create(body);
        }

        configure?.Invoke(request);
        return client.SendAsync(request);
    }

    /// <summary>
    /// Reads a response body as a JSON document for assertions.
    /// </summary>
    private static async Task<JsonDocument> ReadJsonAsync(HttpResponseMessage response)
    {
        var stream = await response.Content.ReadAsStreamAsync();
        return await JsonDocument.ParseAsync(stream);
    }

    /// <summary>
    /// Verifies the common platform error envelope contains message and trace fields.
    /// </summary>
    private static async Task AssertErrorResponseAsync(HttpResponseMessage response)
    {
        var json = await ReadJsonAsync(response);
        Assert.False(string.IsNullOrWhiteSpace(json.RootElement.GetProperty("msg").GetString()));
        Assert.False(string.IsNullOrWhiteSpace(json.RootElement.GetProperty("trace_id").GetString()));
    }

    /// <summary>
    /// Creates a QQ ID that does not collide with deterministic fixture users.
    /// </summary>
    private static string CreateUniqueQqId()
    {
        return "99" + Guid.NewGuid().ToString("N")[..14];
    }

    /// <summary>
    /// Captures login credentials and payload needed by follow-up authenticated assertions.
    /// </summary>
    private sealed record LoginResult(string Token, JsonDocument Json);

    /// <summary>
    /// Hosts the Auth service with test configuration and the shared database.
    /// </summary>
    private sealed class AuthServiceFactory(string connectionString) : WebApplicationFactory<AuthServiceProgram>
    {
        /// <summary>
        /// Injects integration-test configuration before the Auth service starts.
        /// </summary>
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.ConfigureAppConfiguration((_, configuration) =>
            {
                configuration.AddInMemoryCollection(CreateConfiguration(
                    connectionString,
                    SekaiInternalAuthDefaults.AuthServiceActor));
            });
        }
    }

    /// <summary>
    /// Hosts the API service and routes its Auth service client to the in-memory Auth host.
    /// </summary>
    private sealed class ApiServiceFactory(
        string connectionString,
        AuthServiceFactory authFactory) : WebApplicationFactory<Program>
    {
        /// <summary>
        /// Injects configuration and replaces the Auth service HTTP client for test isolation.
        /// </summary>
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.ConfigureAppConfiguration((_, configuration) =>
            {
                configuration.AddInMemoryCollection(CreateConfiguration(
                    connectionString,
                    SekaiInternalAuthDefaults.ApiServiceActor,
                    includePrivateKey: true));
            });

            builder.ConfigureTestServices(services =>
            {
                services.AddHttpClient("auth-service")
                    .ConfigurePrimaryHttpMessageHandler(() => authFactory.Server.CreateHandler());
            });
        }
    }

    /// <summary>
    /// Creates shared service configuration used by the API and Auth test hosts.
    /// </summary>
    private static Dictionary<string, string?> CreateConfiguration(
        string connectionString,
        string actor,
        bool includePrivateKey = false)
    {
        var configuration = new Dictionary<string, string?>
        {
            ["ConnectionStrings:Postgres"] = connectionString,
            ["InternalServices:AuthService"] = "http://auth-service",
            ["InternalServices:AssetService"] = "http://asset-service",
            ["InternalServices:SearchService"] = "http://search-service",
            ["Jwt:Issuer"] = "sekai-platform",
            ["Jwt:Audience"] = "sekai-platform",
            ["Jwt:SigningKey"] = "replace-with-local-development-signing-key",
            ["Database:AutoMigrate"] = "false",
            ["Database:Seed"] = "false"
        };
        IntegrationTestInternalAuth.AddConfiguration(configuration, actor, includePrivateKey);
        return configuration;
    }
}
