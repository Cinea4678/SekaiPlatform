extern alias AssetService;
extern alias OpenApiService;

using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SekaiPlatform.Database;
using SekaiPlatform.Shared.Web.Auth;
using AssetServiceProgram = AssetService::Program;
using OpenApiServiceProgram = OpenApiService::Program;

namespace SekaiPlatform.IntegrationTests;

/// <summary>
/// Exercises anonymous Open API public translation reads through OpenApiService and AssetService.
/// </summary>
[Collection(IntegrationTestCollection.Name)]
public sealed class OpenApiTests : IDisposable
{
    private readonly IntegrationTestDatabaseFixture fixture;
    private readonly AssetServiceFactory assetFactory;
    private readonly OpenApiServiceFactory openApiFactory;

    /// <summary>
    /// Creates Open API and Asset Service hosts wired to the shared integration database.
    /// </summary>
    public OpenApiTests(IntegrationTestDatabaseFixture fixture)
    {
        this.fixture = fixture;
        assetFactory = new AssetServiceFactory(fixture.ConnectionString);
        openApiFactory = new OpenApiServiceFactory(fixture.ConnectionString, assetFactory);
    }

    /// <summary>
    /// Verifies anonymous callers can read published translation info and lines by scenario ID.
    /// </summary>
    [Fact]
    public async Task PublicTranslationGet_ReturnsPublishedTranslationWithLines()
    {
        var seed = await SeedPublicTranslationAsync();
        using var client = openApiFactory.CreateClient();
        var request = new HttpRequestMessage(HttpMethod.Get, $"/api/public/translations/{seed.ScenarioId}");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", "external-token-is-ignored");
        request.Headers.Add("X-Sekai-User-Id", "100");
        request.Headers.Add("X-Sekai-Tenant-Id", "200");

        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var json = await ReadJsonAsync(response);
        Assert.Equal(seed.ScenarioId, json.RootElement.GetProperty("scenario_id").GetString());
        Assert.True(json.RootElement.GetProperty("has_translation").GetBoolean());
        var translation = Assert.Single(json.RootElement.GetProperty("translations").EnumerateArray());
        Assert.Equal(seed.PublishedVersionId, translation.GetProperty("translation_version_id").GetInt64());
        Assert.Equal(2, translation.GetProperty("version_no").GetInt32());
        Assert.Equal("公开译文", translation.GetProperty("title").GetString());
        Assert.Equal(IntegrationTestDatabaseFixture.TenantName, translation.GetProperty("tenant").GetProperty("name").GetString());
        Assert.Equal("翻译A", translation.GetProperty("staff").GetProperty("translator").GetString());
        Assert.Equal("校对B", translation.GetProperty("staff").GetProperty("proofreader").GetString());
        Assert.Equal("合意C", translation.GetProperty("staff").GetProperty("approver").GetString());
        Assert.Equal(2, translation.GetProperty("line_count").GetInt32());
        var lines = translation.GetProperty("lines").EnumerateArray().ToArray();
        Assert.Equal(new[] { 1, 2 }, lines.Select(line => line.GetProperty("line_no").GetInt32()).ToArray());
        Assert.Equal("dialogue", lines[0].GetProperty("line_type").GetString());
        Assert.Equal("译者 1", lines[0].GetProperty("speaker").GetString());
        Assert.Equal("公开译文 1", lines[0].GetProperty("text").GetString());
    }

    /// <summary>
    /// Verifies missing and unpublished scenarios use the non-error no-translation response.
    /// </summary>
    [Fact]
    public async Task PublicTranslationGet_WhenMissingOrUnpublished_ReturnsNoTranslation()
    {
        var seed = await SeedPublicTranslationAsync(published: false);
        using var client = openApiFactory.CreateClient();

        using var unpublished = await client.GetAsync($"/api/public/translations/{seed.ScenarioId}");
        using var missing = await client.GetAsync("/api/public/translations/not_synced_scenario");

        Assert.Equal(HttpStatusCode.OK, unpublished.StatusCode);
        Assert.Equal(HttpStatusCode.OK, missing.StatusCode);
        await AssertNoTranslationAsync(unpublished, seed.ScenarioId);
        await AssertNoTranslationAsync(missing, "not_synced_scenario");
    }

    /// <summary>
    /// Verifies batch reads preserve input order and omit translated line contents.
    /// </summary>
    [Fact]
    public async Task PublicTranslationBatch_ReturnsInfoWithoutLinesInRequestOrder()
    {
        var first = await SeedPublicTranslationAsync();
        var second = await SeedPublicTranslationAsync();
        using var client = openApiFactory.CreateClient();

        using var response = await client.PostAsJsonAsync("/api/public/translations/batch", new
        {
            scenario_ids = new[] { second.ScenarioId, "missing_batch_scenario", first.ScenarioId, second.ScenarioId }
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var json = await ReadJsonAsync(response);
        var items = json.RootElement.GetProperty("items").EnumerateArray().ToArray();
        Assert.Equal(
            new[] { second.ScenarioId, "missing_batch_scenario", first.ScenarioId, second.ScenarioId },
            items.Select(item => item.GetProperty("scenario_id").GetString()).ToArray());
        Assert.True(items[0].GetProperty("has_translation").GetBoolean());
        Assert.False(items[1].GetProperty("has_translation").GetBoolean());
        Assert.True(items[2].GetProperty("has_translation").GetBoolean());
        Assert.False(items[0].GetProperty("translations")[0].TryGetProperty("lines", out _));
    }

    /// <summary>
    /// Verifies batch request validation returns the public Open API error envelope.
    /// </summary>
    [Fact]
    public async Task PublicTranslationBatch_WithInvalidBody_ReturnsBadRequestEnvelope()
    {
        using var client = openApiFactory.CreateClient();

        using var response = await client.PostAsJsonAsync("/api/public/translations/batch", new
        {
            scenario_ids = Array.Empty<string>()
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        await AssertOpenApiErrorAsync(response, "bad_request");

        using var malformed = await client.PostAsync(
            "/api/public/translations/batch",
            new StringContent("{", Encoding.UTF8, "application/json"));

        Assert.Equal(HttpStatusCode.BadRequest, malformed.StatusCode);
        await AssertOpenApiErrorAsync(malformed, "bad_request");
    }

    /// <summary>
    /// Verifies OpenApiService calls AssetService with the dedicated public translation token.
    /// </summary>
    [Fact]
    public async Task OpenApiService_UsesPublicTranslationInternalToken()
    {
        var assetHandler = new FakeAssetServiceHandler();
        using var openApi = new OpenApiServiceFactory(fixture.ConnectionString, assetHandler);
        using var client = openApi.CreateClient();

        using var response = await client.GetAsync("/api/public/translations/token_check");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var token = Assert.Single(assetHandler.InternalTokens);
        AssertPublicTranslationToken(token);
    }

    /// <summary>
    /// Ensures AssetService rejects invalid internal public translation tokens.
    /// </summary>
    [Theory]
    [MemberData(nameof(InvalidInternalPublicTranslationTokens))]
    public async Task AssetPublicTranslationInternalEndpoints_RejectInvalidTokens(
        string? token,
        HttpStatusCode expectedStatus)
    {
        using var client = assetFactory.CreateClient();
        var request = new HttpRequestMessage(HttpMethod.Get, "/internal/public/translations/any_scenario");
        if (token is not null)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        using var response = await client.SendAsync(request);

        Assert.Equal(expectedStatus, response.StatusCode);
    }

    /// <summary>
    /// Verifies Open API rate limiting partitions by forwarded client IP.
    /// </summary>
    [Fact]
    public async Task PublicTranslationEndpoints_RateLimitByForwardedForIp()
    {
        var assetHandler = new FakeAssetServiceHandler();
        using var openApi = new OpenApiServiceFactory(fixture.ConnectionString, assetHandler);
        using var client = openApi.CreateClient();

        for (var i = 0; i < 10; i++)
        {
            using var allowed = await SendWithForwardedForAsync(client, "198.51.100.10");
            Assert.Equal(HttpStatusCode.OK, allowed.StatusCode);
        }

        using var limited = await SendWithForwardedForAsync(client, "198.51.100.10");
        using var otherIp = await SendWithForwardedForAsync(client, "198.51.100.11");

        Assert.Equal(HttpStatusCode.TooManyRequests, limited.StatusCode);
        await AssertOpenApiErrorAsync(limited, "rate_limited");
        Assert.Equal(HttpStatusCode.OK, otherIp.StatusCode);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        openApiFactory.Dispose();
        assetFactory.Dispose();
    }

    /// <summary>
    /// Gets invalid internal tokens for public translation authorization checks.
    /// </summary>
    public static TheoryData<string?, HttpStatusCode> InvalidInternalPublicTranslationTokens()
    {
        return new TheoryData<string?, HttpStatusCode>
        {
            { null, HttpStatusCode.Unauthorized },
            {
                IntegrationTestInternalAuth.Issue(
                    SekaiInternalAuthDefaults.ApiServiceActor,
                    SekaiInternalAuthDefaults.AssetServiceActor,
                    SekaiInternalAuthDefaults.PublicTranslationReadScope),
                HttpStatusCode.Forbidden
            },
            {
                IntegrationTestInternalAuth.Issue(
                    SekaiInternalAuthDefaults.OpenApiServiceActor,
                    SekaiInternalAuthDefaults.AssetServiceActor,
                    SekaiInternalAuthDefaults.AssetsReadScope),
                HttpStatusCode.Forbidden
            }
        };
    }

    private async Task<SeededPublicTranslation> SeedPublicTranslationAsync(bool published = true)
    {
        await using var dbContext = fixture.CreateDbContext();
        var now = DateTimeOffset.UtcNow;
        var suffix = Guid.NewGuid().ToString("N");
        var tenant = await dbContext.Tenants
            .Where(item => item.Name == IntegrationTestDatabaseFixture.TenantName)
            .SingleAsync();
        var adminId = await dbContext.Users
            .Where(user => user.QqId == IntegrationTestDatabaseFixture.AdminQqId)
            .Select(user => user.Id)
            .SingleAsync();
        tenant.AvatarUrl = "https://example.com/tenant.png";

        var group = new StoryGroup
        {
            StoryType = "event_story",
            ExternalType = "event",
            ExternalId = $"public_translation_group_{suffix}",
            DisplayNo = 1,
            Title = "公开译文剧情集",
            CreatedAt = now,
            UpdatedAt = now
        };
        var story = new Story
        {
            Group = group,
            StoryType = "event_story",
            ScenarioId = $"public_translation_{suffix}",
            Title = "公开译文剧情",
            SortOrder = 1,
            CreatedAt = now,
            UpdatedAt = now
        };
        dbContext.StoryGroups.Add(group);
        dbContext.Stories.Add(story);

        var sourceLines = new[]
        {
            new StorySourceLine
            {
                Story = story,
                LineNo = 1,
                LineType = "dialogue",
                Speaker = "原文 1",
                Text = "Source 1",
                CreatedAt = now,
                UpdatedAt = now
            },
            new StorySourceLine
            {
                Story = story,
                LineNo = 2,
                LineType = "scene",
                Text = "Source 2",
                CreatedAt = now,
                UpdatedAt = now
            }
        };
        dbContext.StorySourceLines.AddRange(sourceLines);
        await dbContext.SaveChangesAsync();

        var unpublished = new TranslationVersion
        {
            TenantId = tenant.Id,
            Story = story,
            VersionNo = 1,
            Title = "未公开译文",
            IsPublished = false,
            CreatedBy = adminId,
            CreatedAt = now,
            UpdatedAt = now.AddMinutes(10)
        };
        var version = new TranslationVersion
        {
            TenantId = tenant.Id,
            Story = story,
            VersionNo = 2,
            Title = "公开译文",
            Metadata = """{"staff":{"translator":"翻译A","proofreader":"校对B","approver":"合意C"}}""",
            IsPublished = published,
            CreatedBy = adminId,
            CreatedAt = now,
            UpdatedAt = now.AddMinutes(1)
        };
        dbContext.TranslationVersions.AddRange(unpublished, version);
        dbContext.TranslationLines.AddRange(
            new TranslationLine
            {
                Version = version,
                SourceLineId = sourceLines[0].Id,
                StoryId = story.Id,
                LineNo = 1,
                Speaker = "译者 1",
                Text = "公开译文 1",
                CreatedAt = now,
                UpdatedAt = now
            },
            new TranslationLine
            {
                Version = version,
                SourceLineId = sourceLines[1].Id,
                StoryId = story.Id,
                LineNo = 2,
                Text = "公开译文 2",
                CreatedAt = now,
                UpdatedAt = now
            },
            new TranslationLine
            {
                Version = unpublished,
                SourceLineId = sourceLines[0].Id,
                StoryId = story.Id,
                LineNo = 1,
                Text = "未公开译文",
                CreatedAt = now,
                UpdatedAt = now
            });

        await dbContext.SaveChangesAsync();
        return new SeededPublicTranslation(story.ScenarioId, version.Id);
    }

    private static Task<HttpResponseMessage> SendWithForwardedForAsync(HttpClient client, string ip)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, "/api/public/translations/rate_limit_check");
        request.Headers.Add("X-Forwarded-For", ip);
        return client.SendAsync(request);
    }

    private static async Task AssertNoTranslationAsync(HttpResponseMessage response, string expectedScenarioId)
    {
        var json = await ReadJsonAsync(response);
        Assert.Equal(expectedScenarioId, json.RootElement.GetProperty("scenario_id").GetString());
        Assert.False(json.RootElement.GetProperty("has_translation").GetBoolean());
        Assert.Empty(json.RootElement.GetProperty("translations").EnumerateArray());
    }

    private static async Task AssertOpenApiErrorAsync(HttpResponseMessage response, string expectedCode)
    {
        var json = await ReadJsonAsync(response);
        Assert.Equal(expectedCode, json.RootElement.GetProperty("code").GetString());
        Assert.False(string.IsNullOrWhiteSpace(json.RootElement.GetProperty("message").GetString()));
        Assert.False(string.IsNullOrWhiteSpace(json.RootElement.GetProperty("trace_id").GetString()));
    }

    private static async Task<JsonDocument> ReadJsonAsync(HttpResponseMessage response)
    {
        var stream = await response.Content.ReadAsStreamAsync();
        return await JsonDocument.ParseAsync(stream);
    }

    private static void AssertPublicTranslationToken(string? token)
    {
        Assert.False(string.IsNullOrWhiteSpace(token));
        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(token);
        Assert.Equal(SekaiInternalAuthDefaults.OpenApiServiceActor, jwt.Claims.Single(claim =>
            claim.Type == SekaiInternalAuthDefaults.ActorClaimType).Value);
        Assert.Equal(SekaiInternalAuthDefaults.PublicTranslationReadScope, jwt.Claims.Single(claim =>
            claim.Type == SekaiInternalAuthDefaults.ScopeClaimType).Value);
        Assert.Contains(SekaiInternalAuthDefaults.AssetServiceActor, jwt.Audiences);
        Assert.DoesNotContain(jwt.Claims, claim => claim.Type == SekaiAuthDefaults.TenantIdClaimType);
        Assert.DoesNotContain(jwt.Claims, claim => claim.Type == SekaiInternalAuthDefaults.SubjectUserIdClaimType);
    }

    /// <summary>
    /// Hosts AssetService with test configuration and the shared database.
    /// </summary>
    private sealed class AssetServiceFactory(string connectionString) : WebApplicationFactory<AssetServiceProgram>
    {
        /// <summary>
        /// Injects integration-test configuration before AssetService starts.
        /// </summary>
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.ConfigureAppConfiguration((_, configuration) =>
            {
                configuration.AddInMemoryCollection(CreateConfiguration(
                    connectionString,
                    SekaiInternalAuthDefaults.AssetServiceActor,
                    includePrivateKey: true));
            });
        }
    }

    /// <summary>
    /// Hosts OpenApiService and routes internal AssetService calls to test handlers.
    /// </summary>
    private sealed class OpenApiServiceFactory : WebApplicationFactory<OpenApiServiceProgram>
    {
        private readonly string connectionString;
        private readonly AssetServiceFactory? assetFactory;
        private readonly HttpMessageHandler? assetHandler;

        /// <summary>
        /// Creates an OpenApiService host backed by a real in-memory AssetService.
        /// </summary>
        public OpenApiServiceFactory(string connectionString, AssetServiceFactory assetFactory)
        {
            this.connectionString = connectionString;
            this.assetFactory = assetFactory;
        }

        /// <summary>
        /// Creates an OpenApiService host backed by a fake AssetService handler.
        /// </summary>
        public OpenApiServiceFactory(string connectionString, HttpMessageHandler assetHandler)
        {
            this.connectionString = connectionString;
            this.assetHandler = assetHandler;
        }

        /// <summary>
        /// Injects configuration and replaces internal service HTTP clients for test isolation.
        /// </summary>
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.ConfigureAppConfiguration((_, configuration) =>
            {
                configuration.AddInMemoryCollection(CreateConfiguration(
                    connectionString,
                    SekaiInternalAuthDefaults.OpenApiServiceActor,
                    includePrivateKey: true));
            });

            builder.ConfigureTestServices(services =>
            {
                services.AddHttpClient("asset-service")
                    .ConfigurePrimaryHttpMessageHandler(() =>
                        assetHandler ?? assetFactory!.Server.CreateHandler());
            });
        }
    }

    /// <summary>
    /// Captures OpenApiService calls to AssetService for internal-token assertions.
    /// </summary>
    private sealed class FakeAssetServiceHandler : HttpMessageHandler
    {
        /// <summary>
        /// Gets captured internal bearer tokens sent by OpenApiService.
        /// </summary>
        public List<string?> InternalTokens { get; } = [];

        /// <summary>
        /// Records public translation requests and returns a successful no-translation body.
        /// </summary>
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            InternalTokens.Add(request.Headers.Authorization?.Parameter);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    """{"scenario_id":"token_check","has_translation":false,"translations":[]}""",
                    Encoding.UTF8,
                    "application/json")
            });
        }
    }

    /// <summary>
    /// Creates shared service configuration used by OpenApiService and AssetService test hosts.
    /// </summary>
    private static Dictionary<string, string?> CreateConfiguration(
        string connectionString,
        string actor,
        bool includePrivateKey = false)
    {
        var configuration = new Dictionary<string, string?>
        {
            ["ConnectionStrings:Postgres"] = connectionString,
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

    /// <summary>
    /// Captures seeded public translation identifiers.
    /// </summary>
    private sealed record SeededPublicTranslation(string ScenarioId, long PublishedVersionId);
}
