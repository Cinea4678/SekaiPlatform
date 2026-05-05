extern alias AssetService;
extern alias AuthService;

using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.IdentityModel.Tokens.Jwt;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SekaiPlatform.Database;
using SekaiPlatform.Shared.Web.Auth;
using SekaiPlatform.Shared.Web.Search;
using SekaiPlatform.SourceSync;
using SekaiPlatform.SourceSync.Catalog;
using AssetServiceProgram = AssetService::Program;
using AuthServiceProgram = AuthService::Program;

namespace SekaiPlatform.IntegrationTests;

/// <summary>
/// Exercises manual source-story synchronization through API, Auth, Asset, and sync runner paths.
/// </summary>
[Collection(IntegrationTestCollection.Name)]
public sealed class SyncApiTests : IDisposable
{
    private readonly IntegrationTestDatabaseFixture fixture;
    private readonly AuthServiceFactory authFactory;
    private readonly AssetServiceFactory assetFactory;
    private readonly ApiServiceFactory apiFactory;
    private readonly FakeSearchIndexHandler searchIndexHandler = new();

    /// <summary>
    /// Creates service hosts wired to a fake Moe Sekai source and the shared database.
    /// </summary>
    public SyncApiTests(IntegrationTestDatabaseFixture fixture)
    {
        this.fixture = fixture;
        authFactory = new AuthServiceFactory(fixture.ConnectionString);
        assetFactory = new AssetServiceFactory(
            fixture.ConnectionString,
            new FakeMoeSekaiHandler("scenario_event_success", scenarioSucceeds: true),
            searchIndexHandler);
        apiFactory = new ApiServiceFactory(fixture.ConnectionString, authFactory, assetFactory);
    }

    /// <summary>
    /// Verifies an administrator can trigger sync and persist story groups, stories, source lines, and job state.
    /// </summary>
    [Fact]
    public async Task ManualSync_AdminCreatesStoriesSourceLinesAndSucceededJob()
    {
        using var client = apiFactory.CreateClient();
        var login = await LoginAsync(
            client,
            IntegrationTestDatabaseFixture.AdminQqId,
            IntegrationTestDatabaseFixture.AdminPassword);

        using var response = await SendWithBearerAsync(
            client,
            HttpMethod.Post,
            "/api/sync/jobs",
            login.Token,
            new { source = "moesekai" });

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        var json = await ReadJsonAsync(response);
        var syncJobId = json.RootElement.GetProperty("id").GetInt64();
        Assert.Equal("source_story_sync", json.RootElement.GetProperty("job_type").GetString());
        Assert.Equal("manual", json.RootElement.GetProperty("trigger_type").GetString());
        Assert.Equal("pending", json.RootElement.GetProperty("status").GetString());
        Assert.Equal(JsonValueKind.Object, json.RootElement.GetProperty("metadata").ValueKind);

        var job = await WaitForSyncJobAsync(syncJobId, SourceSyncConstants.StatusSucceeded);
        using var metadata = JsonDocument.Parse(job.Metadata!);
        Assert.Equal(1, metadata.RootElement.GetProperty("story_count").GetInt32());

        await using var dbContext = fixture.CreateDbContext();
        var story = await dbContext.Stories.SingleAsync(item => item.ScenarioId == "scenario_event_success");
        Assert.Equal(SourceSyncConstants.EventStory, story.StoryType);
        Assert.Equal(3, await dbContext.StorySourceLines.CountAsync(item => item.StoryId == story.Id));
        await WaitForSearchRefreshAsync();
        AssertStoryRefreshRequested(searchIndexHandler, story.Id);
        Assert.All(searchIndexHandler.InternalTokens, AssertSearchIndexRefreshToken);

        using var list = await SendWithBearerAsync(client, HttpMethod.Get, "/api/sync/jobs?limit=1", login.Token);
        Assert.Equal(HttpStatusCode.OK, list.StatusCode);
    }

    /// <summary>
    /// Ensures normal users cannot trigger manual source synchronization.
    /// </summary>
    [Fact]
    public async Task ManualSync_NormalUserIsForbidden()
    {
        using var client = apiFactory.CreateClient();
        var login = await LoginAsync(
            client,
            IntegrationTestDatabaseFixture.NormalUserQqId,
            IntegrationTestDatabaseFixture.NormalUserPassword);

        using var response = await SendWithBearerAsync(
            client,
            HttpMethod.Post,
            "/api/sync/jobs",
            login.Token,
            new { source = "moesekai" });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        await AssertErrorResponseAsync(response);
    }

    /// <summary>
    /// Verifies one failed scenario is recorded in metadata while successful scenarios are still committed.
    /// </summary>
    [Fact]
    public async Task PartialScenarioFailureKeepsJobSucceededAndRecordsMetadata()
    {
        await using var dbContext = fixture.CreateDbContext();
        var handler = new FakeMoeSekaiHandler(
            "scenario_event_success_partial",
            scenarioSucceeds: true,
            "scenario_event_missing_partial",
            extraScenarioSucceeds: false);
        var options = handler.CreateOptions();
        var runner = new SourceStorySyncRunner(
            dbContext,
            new MoeSekaiMasterClient(new HttpClient(handler), options),
            new MoeSekaiScenarioClient(new HttpClient(handler), options),
            new MoeSekaiCatalogBuilder(),
            new UnityScenarioParser(),
            options);

        var job = await runner.RunAsync(SourceSyncConstants.TriggerManual, CancellationToken.None);

        Assert.Equal(SourceSyncConstants.StatusSucceeded, job.Status);
        using var metadata = JsonDocument.Parse(job.Metadata!);
        Assert.Equal(1, metadata.RootElement.GetProperty("failed_scenario_count").GetInt32());
        Assert.Equal(3, await dbContext.StorySourceLines.CountAsync(line =>
            line.Story!.ScenarioId == "scenario_event_success_partial"));
        Assert.Equal(0, await dbContext.StorySourceLines.CountAsync(line =>
            line.Story!.ScenarioId == "scenario_event_missing_partial"));
    }

    /// <summary>
    /// Verifies an unchanged story with existing source lines does not download its scenario again.
    /// </summary>
    [Fact]
    public async Task ScenarioUnchangedWithSourceLines_SkipsScenarioDownload()
    {
        await using var dbContext = fixture.CreateDbContext();
        var initialHandler = new FakeMoeSekaiHandler("scenario_event_skip", scenarioSucceeds: true);
        var initialOptions = initialHandler.CreateOptions();
        var initialRunner = new SourceStorySyncRunner(
            dbContext,
            new MoeSekaiMasterClient(new HttpClient(initialHandler), initialOptions),
            new MoeSekaiScenarioClient(new HttpClient(initialHandler), initialOptions),
            new MoeSekaiCatalogBuilder(),
            new UnityScenarioParser(),
            initialOptions);

        var initialJob = await initialRunner.RunAsync(SourceSyncConstants.TriggerManual, CancellationToken.None);

        Assert.Equal(SourceSyncConstants.StatusSucceeded, initialJob.Status);
        Assert.Equal(1, initialHandler.ScenarioRequestCount);
        Assert.Equal(3, await dbContext.StorySourceLines.CountAsync(line =>
            line.Story!.ScenarioId == "scenario_event_skip"));

        var skipHandler = new FakeMoeSekaiHandler("scenario_event_skip", scenarioSucceeds: false);
        var skipOptions = skipHandler.CreateOptions();
        var skipRunner = new SourceStorySyncRunner(
            dbContext,
            new MoeSekaiMasterClient(new HttpClient(skipHandler), skipOptions),
            new MoeSekaiScenarioClient(new HttpClient(skipHandler), skipOptions),
            new MoeSekaiCatalogBuilder(),
            new UnityScenarioParser(),
            skipOptions);

        var skippedJob = await skipRunner.RunAsync(SourceSyncConstants.TriggerManual, CancellationToken.None);

        Assert.Equal(SourceSyncConstants.StatusSucceeded, skippedJob.Status);
        Assert.Equal(0, skipHandler.ScenarioRequestCount);
        using var metadata = JsonDocument.Parse(skippedJob.Metadata!);
        Assert.Equal(0, metadata.RootElement.GetProperty("synced_story_count").GetInt32());
        Assert.Equal(1, metadata.RootElement.GetProperty("skipped_story_count").GetInt32());
        Assert.Equal(0, metadata.RootElement.GetProperty("failed_scenario_count").GetInt32());
        Assert.Equal(3, await dbContext.StorySourceLines.CountAsync(line =>
            line.Story!.ScenarioId == "scenario_event_skip"));
    }

    /// <summary>
    /// Ensures a sync with no downloadable scenarios marks the job as failed.
    /// </summary>
    [Fact]
    public async Task ScenarioTotalFailureMarksJobFailed()
    {
        await using var dbContext = fixture.CreateDbContext();
        var handler = new FakeMoeSekaiHandler("scenario_event_total_missing", scenarioSucceeds: false);
        var options = handler.CreateOptions();
        var runner = new SourceStorySyncRunner(
            dbContext,
            new MoeSekaiMasterClient(new HttpClient(handler), options),
            new MoeSekaiScenarioClient(new HttpClient(handler), options),
            new MoeSekaiCatalogBuilder(),
            new UnityScenarioParser(),
            options);

        var job = await runner.RunAsync(SourceSyncConstants.TriggerManual, CancellationToken.None);

        Assert.Equal(SourceSyncConstants.StatusFailed, job.Status);
        Assert.Equal("原文同步失败。", job.ErrorMessage);
    }

    /// <summary>
    /// Verifies malformed manual sync JSON is rejected with the common error envelope.
    /// </summary>
    [Fact]
    public async Task ManualSync_WithMalformedJson_ReturnsBadRequest()
    {
        using var client = apiFactory.CreateClient();
        var login = await LoginAsync(
            client,
            IntegrationTestDatabaseFixture.AdminQqId,
            IntegrationTestDatabaseFixture.AdminPassword);

        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/sync/jobs")
        {
            Content = new StringContent("{", Encoding.UTF8, "application/json")
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", login.Token);

        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        await AssertErrorResponseAsync(response);
    }

    /// <summary>
    /// Ensures Asset Service internal sync endpoints reject requests without an internal token.
    /// </summary>
    [Fact]
    public async Task InternalSyncJobs_WithoutInternalToken_ReturnsUnauthorized()
    {
        using var client = assetFactory.CreateClient();

        using var response = await client.GetAsync("/internal/sync/jobs");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    /// <summary>
    /// Ensures tenant-scoped Asset Service endpoints require a delegated tenant claim.
    /// </summary>
    [Fact]
    public async Task InternalSyncJobs_WithoutTenantClaim_ReturnsForbidden()
    {
        using var client = assetFactory.CreateClient();
        var request = new HttpRequestMessage(HttpMethod.Get, "/internal/sync/jobs");
        request.Headers.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            IntegrationTestInternalAuth.Issue(
                SekaiInternalAuthDefaults.ApiServiceActor,
                SekaiInternalAuthDefaults.AssetServiceActor,
                SekaiInternalAuthDefaults.SyncJobsReadScope,
                subjectUserId: 1));

        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    /// <summary>
    /// Disposes API, Asset, and Auth hosts created for the test case.
    /// </summary>
    public void Dispose()
    {
        apiFactory.Dispose();
        assetFactory.Dispose();
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
        object? body = null)
    {
        var request = new HttpRequestMessage(method, path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        if (body is not null)
        {
            request.Content = JsonContent.Create(body);
        }

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
    /// Waits until a background sync job reaches the expected status.
    /// </summary>
    private async Task<SyncJob> WaitForSyncJobAsync(long syncJobId, string expectedStatus)
    {
        var deadline = DateTimeOffset.UtcNow.AddSeconds(10);
        while (DateTimeOffset.UtcNow < deadline)
        {
            await using var dbContext = fixture.CreateDbContext();
            var job = await dbContext.SyncJobs.AsNoTracking().SingleAsync(item => item.Id == syncJobId);
            if (job.Status == expectedStatus)
            {
                return job;
            }

            await Task.Delay(50);
        }

        throw new TimeoutException($"Sync job {syncJobId} did not reach {expectedStatus}.");
    }

    /// <summary>
    /// Waits until the background sync has requested a search index refresh.
    /// </summary>
    private async Task WaitForSearchRefreshAsync()
    {
        var deadline = DateTimeOffset.UtcNow.AddSeconds(10);
        while (DateTimeOffset.UtcNow < deadline)
        {
            if (searchIndexHandler.RebuildBodies.Count > 0)
            {
                return;
            }

            await Task.Delay(50);
        }

        throw new TimeoutException("Search index refresh was not requested.");
    }

    /// <summary>
    /// Verifies that Asset Service requested an all-scope search refresh for the synchronized story.
    /// </summary>
    private static void AssertStoryRefreshRequested(FakeSearchIndexHandler searchIndexHandler, long storyId)
    {
        var body = Assert.Single(searchIndexHandler.RebuildBodies);
        using var json = JsonDocument.Parse(body);
        Assert.Equal("all", json.RootElement.GetProperty("scope").GetString());
        Assert.Contains(
            storyId,
            json.RootElement.GetProperty("story_ids").EnumerateArray().Select(item => item.GetInt64()));
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
    /// Hosts the Asset service with fake Moe Sekai clients for deterministic sync tests.
    /// </summary>
    private sealed class AssetServiceFactory(
        string connectionString,
        FakeMoeSekaiHandler handler,
        FakeSearchIndexHandler searchIndexHandler) : WebApplicationFactory<AssetServiceProgram>
    {
        /// <summary>
        /// Injects configuration and replaces upstream Moe Sekai clients with fake HTTP clients.
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

            builder.ConfigureTestServices(services =>
            {
                var options = handler.CreateOptions();
                services.AddSingleton(options);
                services.AddSingleton(new MoeSekaiMasterClient(new HttpClient(handler), options));
                services.AddSingleton(new MoeSekaiScenarioClient(new HttpClient(handler), options));
                services.AddHttpClient<SearchIndexRefreshClient>()
                    .ConfigurePrimaryHttpMessageHandler(() => searchIndexHandler);
            });
        }
    }

    /// <summary>
    /// Captures search index refresh calls made by Asset Service after successful sync.
    /// </summary>
    private sealed class FakeSearchIndexHandler : HttpMessageHandler
    {
        /// <summary>
        /// Gets captured search index rebuild request bodies.
        /// </summary>
        public List<string> RebuildBodies { get; } = [];

        /// <summary>
        /// Gets captured internal bearer tokens sent by Asset Service.
        /// </summary>
        public List<string?> InternalTokens { get; } = [];

        /// <summary>
        /// Records rebuild requests and returns a successful maintenance response.
        /// </summary>
        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            if (request.RequestUri?.AbsolutePath == "/internal/search/index/rebuild")
            {
                InternalTokens.Add(request.Headers.Authorization?.Parameter);
                RebuildBodies.Add(request.Content is null
                    ? ""
                    : await request.Content.ReadAsStringAsync(cancellationToken));
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(
                        """{"scope":"all","deleted":true,"source_indexed":3,"translation_indexed":0}""",
                        Encoding.UTF8,
                        "application/json")
                };
            }

            return new HttpResponseMessage(HttpStatusCode.NotFound);
        }
    }

    /// <summary>
    /// Hosts the API service and routes internal Auth and Asset clients to in-memory hosts.
    /// </summary>
    private sealed class ApiServiceFactory(
        string connectionString,
        AuthServiceFactory authFactory,
        AssetServiceFactory assetFactory) : WebApplicationFactory<Program>
    {
        /// <summary>
        /// Injects configuration and replaces internal service HTTP clients for test isolation.
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
                services.AddHttpClient("asset-service")
                    .ConfigurePrimaryHttpMessageHandler(() => assetFactory.Server.CreateHandler());
            });
        }
    }

    /// <summary>
    /// Provides deterministic Moe Sekai master and asset responses for source sync tests.
    /// </summary>
    private sealed class FakeMoeSekaiHandler(
        string scenarioId,
        bool scenarioSucceeds,
        string? extraScenarioId = null,
        bool extraScenarioSucceeds = false) : HttpMessageHandler
    {
        /// <summary>
        /// Creates source sync options that route all upstream calls to this fake handler.
        /// </summary>
        public MoeSekaiSourceSyncOptions CreateOptions()
        {
            return new MoeSekaiSourceSyncOptions
            {
                MasterBaseUrls = ["http://moesekai.test/master/"],
                VersionUrls = ["http://moesekai.test/versions/current_version.json"],
                AssetBaseUrls = ["http://moesekai.test/assets/"]
            };
        }

        /// <summary>
        /// Returns fake version, master catalog, and scenario payloads based on request path.
        /// </summary>
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            var path = request.RequestUri?.AbsolutePath ?? "";
            if (path == "/versions/current_version.json")
            {
                return JsonAsync("""{"dataVersion":"test-master","assetVersion":"test-asset"}""");
            }

            if (path == "/master/events.json")
            {
                return JsonAsync("""[{"id":901001,"name":"测试活动","assetbundleName":"event_story_901001"}]""");
            }

            if (path == "/master/eventStories.json")
            {
                var extraEpisode = extraScenarioId is null
                    ? ""
                    : $$"""
                      ,{
                        "id": 2,
                        "eventStoryId": 901001,
                        "episodeNo": 2,
                        "title": "第2话",
                        "assetbundleName": "event_story_901001",
                        "scenarioId": "{{extraScenarioId}}",
                        "releaseConditionId": 1
                      }
                    """;
                return JsonAsync($$"""
                [{
                  "id": 901001,
                  "eventId": 901001,
                  "outline": "测试活动剧情",
                  "assetbundleName": "event_story_901001",
                  "eventStoryEpisodes": [{
                    "id": 1,
                    "eventStoryId": 901001,
                    "episodeNo": 1,
                    "title": "第1话",
                    "assetbundleName": "event_story_901001",
                    "scenarioId": "{{scenarioId}}",
                    "releaseConditionId": 1
                  }{{extraEpisode}}]
                }]
                """);
            }

            if (path.StartsWith("/master/", StringComparison.Ordinal))
            {
                return JsonAsync("[]");
            }

            if (path.StartsWith("/assets/", StringComparison.Ordinal))
            {
                ScenarioRequestCount++;
            }

            if ((path == $"/assets/event_story/event_story_901001/scenario/{scenarioId}.json" && scenarioSucceeds)
                || (path == $"/assets/event_story/event_story_901001/scenario/{extraScenarioId}.json" && extraScenarioSucceeds))
            {
                var matchedScenarioId = path.Contains(scenarioId, StringComparison.Ordinal) ? scenarioId : extraScenarioId;
                return JsonAsync($$"""
                {
                  "ScenarioId": "{{matchedScenarioId}}",
                  "Snippets": [
                    { "Index": 0, "Action": 1, "ReferenceIndex": 0 },
                    { "Index": 1, "Action": 6, "ReferenceIndex": 0 }
                  ],
                  "TalkData": [{
                    "TalkCharacters": [],
                    "WindowDisplayName": "ミク",
                    "Body": "こんにちは",
                    "Voices": [{ "VoiceId": "voice_001", "Volume": 100 }],
                    "WhenFinishCloseWindow": 1
                  }],
                  "SpecialEffectData": [{ "EffectType": 18, "StringVal": "教室", "StringValSub": "", "IntVal": 0 }]
                }
                """);
            }

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
        }

        /// <summary>
        /// Gets the number of scenario asset requests received by this fake handler.
        /// </summary>
        public int ScenarioRequestCount { get; private set; }

        /// <summary>
        /// Builds a JSON HTTP response for fake upstream payloads.
        /// </summary>
        private static Task<HttpResponseMessage> JsonAsync(string json)
        {
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            });
        }
    }

    /// <summary>
    /// Creates shared service configuration used by API, Auth, and Asset test hosts.
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

    private static void AssertSearchIndexRefreshToken(string? token)
    {
        Assert.False(string.IsNullOrWhiteSpace(token));
        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(token);
        Assert.Equal(SekaiInternalAuthDefaults.AssetServiceActor, jwt.Claims.Single(claim =>
            claim.Type == SekaiInternalAuthDefaults.ActorClaimType).Value);
        Assert.Equal(SekaiInternalAuthDefaults.SearchIndexRebuildScope, jwt.Claims.Single(claim =>
            claim.Type == SekaiInternalAuthDefaults.ScopeClaimType).Value);
        Assert.Contains(SekaiInternalAuthDefaults.SearchServiceActor, jwt.Audiences);
    }
}
