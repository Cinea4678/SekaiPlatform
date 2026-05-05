extern alias AuthService;

using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SekaiPlatform.Shared.Web.Auth;
using AuthServiceProgram = AuthService::Program;

namespace SekaiPlatform.IntegrationTests;

/// <summary>
/// Verifies the public search index rebuild administration API.
/// </summary>
[Collection(IntegrationTestCollection.Name)]
public sealed class SearchIndexAdminApiTests : IDisposable
{
    private readonly AuthServiceFactory authFactory;
    private readonly FakeSearchServiceHandler searchServiceHandler = new();
    private readonly ApiServiceFactory apiFactory;

    /// <summary>
    /// Creates API and Auth hosts wired to a fake Search Service.
    /// </summary>
    public SearchIndexAdminApiTests(IntegrationTestDatabaseFixture fixture)
    {
        authFactory = new AuthServiceFactory(fixture.ConnectionString);
        apiFactory = new ApiServiceFactory(fixture.ConnectionString, authFactory, searchServiceHandler);
    }

    /// <summary>
    /// Ensures a super administrator can request a search index rebuild through API Service.
    /// </summary>
    [Fact]
    public async Task RebuildSearchIndex_SuperAdmin_ForwardsInternalRequest()
    {
        using var client = apiFactory.CreateClient();
        var login = await LoginAsync(
            client,
            IntegrationTestDatabaseFixture.AdminQqId,
            IntegrationTestDatabaseFixture.AdminPassword);

        using var response = await SendWithBearerAsync(
            client,
            "/api/search/index/rebuild",
            login.Token,
            new { scope = "source" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var responseJson = await ReadJsonAsync(response);
        Assert.Equal("source", responseJson.RootElement.GetProperty("scope").GetString());

        var body = Assert.Single(searchServiceHandler.RequestBodies);
        using var bodyJson = JsonDocument.Parse(body);
        Assert.Equal("source", bodyJson.RootElement.GetProperty("scope").GetString());
        Assert.All(searchServiceHandler.InternalTokens, AssertSearchIndexRebuildToken);
    }

    /// <summary>
    /// Ensures tenant administrators below super administrator cannot rebuild search indexes.
    /// </summary>
    [Fact]
    public async Task RebuildSearchIndex_TenantAdmin_ReturnsForbidden()
    {
        using var client = apiFactory.CreateClient();
        var login = await LoginAsync(
            client,
            IntegrationTestDatabaseFixture.TenantAdminQqId,
            IntegrationTestDatabaseFixture.TenantAdminPassword);

        using var response = await SendWithBearerAsync(
            client,
            "/api/search/index/rebuild",
            login.Token,
            new { scope = "source" });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Empty(searchServiceHandler.RequestBodies);
    }

    /// <summary>
    /// Disposes API and Auth hosts created for the test case.
    /// </summary>
    public void Dispose()
    {
        apiFactory.Dispose();
        authFactory.Dispose();
    }

    private static async Task<LoginResult> LoginAsync(HttpClient client, string username, string password)
    {
        using var response = await client.PostAsJsonAsync("/api/auth/login", new { username, password });
        response.EnsureSuccessStatusCode();

        var json = await ReadJsonAsync(response);
        var token = json.RootElement.GetProperty("access_token").GetString();
        Assert.False(string.IsNullOrWhiteSpace(token));

        return new LoginResult(token!);
    }

    private static Task<HttpResponseMessage> SendWithBearerAsync(
        HttpClient client,
        string path,
        string token,
        object body)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, path)
        {
            Content = JsonContent.Create(body)
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client.SendAsync(request);
    }

    private static async Task<JsonDocument> ReadJsonAsync(HttpResponseMessage response)
    {
        var stream = await response.Content.ReadAsStreamAsync();
        return await JsonDocument.ParseAsync(stream);
    }

    private static void AssertSearchIndexRebuildToken(string? token)
    {
        Assert.False(string.IsNullOrWhiteSpace(token));
        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(token);
        Assert.Equal(SekaiInternalAuthDefaults.ApiServiceActor, jwt.Claims.Single(claim =>
            claim.Type == SekaiInternalAuthDefaults.ActorClaimType).Value);
        Assert.Equal(SekaiInternalAuthDefaults.SearchIndexRebuildScope, jwt.Claims.Single(claim =>
            claim.Type == SekaiInternalAuthDefaults.ScopeClaimType).Value);
        Assert.Contains(SekaiInternalAuthDefaults.SearchServiceActor, jwt.Audiences);
    }

    private sealed record LoginResult(string Token);

    private sealed class AuthServiceFactory(string connectionString) : WebApplicationFactory<AuthServiceProgram>
    {
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

    private sealed class ApiServiceFactory(
        string connectionString,
        AuthServiceFactory authFactory,
        FakeSearchServiceHandler searchServiceHandler) : WebApplicationFactory<Program>
    {
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
                services.AddHttpClient("search-service")
                    .ConfigurePrimaryHttpMessageHandler(() => searchServiceHandler);
            });
        }
    }

    private sealed class FakeSearchServiceHandler : HttpMessageHandler
    {
        public List<string> RequestBodies { get; } = [];

        public List<string?> InternalTokens { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            if (request.RequestUri?.AbsolutePath == "/internal/search/index/rebuild")
            {
                InternalTokens.Add(request.Headers.Authorization?.Parameter);
                RequestBodies.Add(request.Content is null
                    ? ""
                    : await request.Content.ReadAsStringAsync(cancellationToken));
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(
                        """{"scope":"source","deleted":true,"source_indexed":3,"translation_indexed":0}""",
                        Encoding.UTF8,
                        "application/json")
                };
            }

            return new HttpResponseMessage(HttpStatusCode.NotFound);
        }
    }

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
