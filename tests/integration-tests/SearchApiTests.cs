extern alias AuthService;
extern alias SearchService;

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
using AuthServiceProgram = AuthService::Program;
using ElasticsearchSearchClient = SearchService::SekaiPlatform.SearchService.Search.ElasticsearchSearchClient;
using SearchServiceProgram = SearchService::Program;

namespace SekaiPlatform.IntegrationTests;

/// <summary>
/// Exercises Phase 6 search API behavior through API Service and Search Service.
/// </summary>
[Collection(IntegrationTestCollection.Name)]
public sealed class SearchApiTests : IDisposable
{
    private readonly IntegrationTestDatabaseFixture fixture;
    private readonly AuthServiceFactory authFactory;
    private readonly FakeElasticsearchSearchHandler elasticsearch = new();
    private readonly SearchServiceFactory searchFactory;
    private readonly ApiServiceFactory apiFactory;
    private readonly List<string?> searchServiceInternalTokens = [];

    /// <summary>
    /// Creates service hosts wired to fake Elasticsearch and the shared integration database.
    /// </summary>
    public SearchApiTests(IntegrationTestDatabaseFixture fixture)
    {
        this.fixture = fixture;
        authFactory = new AuthServiceFactory(fixture.ConnectionString);
        searchFactory = new SearchServiceFactory(fixture.ConnectionString, elasticsearch);
        apiFactory = new ApiServiceFactory(
            fixture.ConnectionString,
            authFactory,
            searchFactory,
            searchServiceInternalTokens);
    }

    /// <summary>
    /// Verifies Search Service queries shared source text and only the current tenant's translations.
    /// </summary>
    [Fact]
    public async Task InternalSearch_ReturnsPagedHitsAndAppliesTenantFilter()
    {
        var activeContext = await GetActiveTenantContextAsync();
        await using var factory = new SearchServiceFactory(fixture.ConnectionString, elasticsearch);
        using var client = factory.CreateClient();
        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            "/internal/search?keyword=%E3%81%93%E3%82%93&page=2&page_size=5");
        request.Headers.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            IntegrationTestInternalAuth.Issue(
                SekaiInternalAuthDefaults.ApiServiceActor,
                SekaiInternalAuthDefaults.SearchServiceActor,
                SekaiInternalAuthDefaults.SearchQueryScope,
                activeContext.UserId,
                activeContext.TenantId));

        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var json = await ReadJsonAsync(response);
        Assert.Equal(2, json.RootElement.GetProperty("total").GetInt64());
        Assert.Equal(2, json.RootElement.GetProperty("page").GetInt32());
        Assert.Equal(5, json.RootElement.GetProperty("page_size").GetInt32());
        Assert.Equal("<mark>こんにちは</mark>", json.RootElement.GetProperty("items")[0]
            .GetProperty("highlight_text")
            .GetString());

        AssertSearchBody(Assert.Single(elasticsearch.SearchBodies), activeContext.TenantId);
    }

    /// <summary>
    /// Verifies search results include paired source text and current-tenant translation lines.
    /// </summary>
    [Fact]
    public async Task InternalSearch_ReturnsPairedSourceAndTenantTranslations()
    {
        var activeContext = await GetActiveTenantContextAsync();
        var seed = await SeedSearchPairAsync(activeContext);
        elasticsearch.SearchResponseJson = CreateSearchPairResponse(seed, activeContext.TenantId);
        await using var factory = new SearchServiceFactory(fixture.ConnectionString, elasticsearch);
        using var client = factory.CreateClient();
        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            "/internal/search?keyword=%E5%8E%9F%E6%96%87&page=1&page_size=20");
        request.Headers.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            IntegrationTestInternalAuth.Issue(
                SekaiInternalAuthDefaults.ApiServiceActor,
                SekaiInternalAuthDefaults.SearchServiceActor,
                SekaiInternalAuthDefaults.SearchQueryScope,
                activeContext.UserId,
                activeContext.TenantId));

        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var json = await ReadJsonAsync(response);
        var sourceHit = json.RootElement.GetProperty("items")[0];
        Assert.Equal("source", sourceHit.GetProperty("asset_type").GetString());
        Assert.Equal(seed.SourceLineId, sourceHit.GetProperty("source").GetProperty("source_line_id").GetInt64());
        Assert.Equal("原文配对测试", sourceHit.GetProperty("source").GetProperty("text").GetString());
        var sourceTranslations = sourceHit.GetProperty("translations").EnumerateArray().ToArray();
        Assert.Equal(2, sourceTranslations.Length);
        Assert.Contains(sourceTranslations, item =>
            item.GetProperty("translation_line_id").GetInt64() == seed.FirstTranslationLineId
            && item.GetProperty("text").GetString() == "译文配对测试一");
        Assert.DoesNotContain(sourceTranslations, item => item.GetProperty("text").GetString() == "其他租户译文");

        var translationHit = json.RootElement.GetProperty("items")[1];
        Assert.Equal("translation", translationHit.GetProperty("asset_type").GetString());
        Assert.Equal(seed.FirstTranslationLineId, translationHit.GetProperty("translation_line_id").GetInt64());
        Assert.Equal("原文配对测试", translationHit.GetProperty("source").GetProperty("text").GetString());
        Assert.Equal(2, translationHit.GetProperty("translations").GetArrayLength());
    }

    /// <summary>
    /// Ensures Search Service requires a delegated tenant claim for query endpoints.
    /// </summary>
    [Fact]
    public async Task InternalSearch_WithoutTenantClaim_ReturnsForbidden()
    {
        using var client = searchFactory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/internal/search?keyword=test");
        request.Headers.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            IntegrationTestInternalAuth.Issue(
                SekaiInternalAuthDefaults.ApiServiceActor,
                SekaiInternalAuthDefaults.SearchServiceActor,
                SekaiInternalAuthDefaults.SearchQueryScope,
                subjectUserId: 101));

        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Empty(elasticsearch.SearchBodies);
    }

    /// <summary>
    /// Ensures stale delegated tenant claims cannot search after membership access is lost.
    /// </summary>
    [Fact]
    public async Task InternalSearch_WithoutActiveMembership_ReturnsForbidden()
    {
        var tenantId = await GetPrimaryTenantIdAsync();
        using var client = searchFactory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/internal/search?keyword=test");
        request.Headers.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            IntegrationTestInternalAuth.Issue(
                SekaiInternalAuthDefaults.ApiServiceActor,
                SekaiInternalAuthDefaults.SearchServiceActor,
                SekaiInternalAuthDefaults.SearchQueryScope,
                subjectUserId: long.MaxValue,
                tenantId: tenantId));

        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Empty(elasticsearch.SearchBodies);
    }

    /// <summary>
    /// Ensures unsupported deep offset pagination is rejected before Elasticsearch is queried.
    /// </summary>
    [Fact]
    public async Task InternalSearch_WhenPageExceedsResultWindow_ReturnsBadRequest()
    {
        var activeContext = await GetActiveTenantContextAsync();
        using var client = searchFactory.CreateClient();
        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            "/internal/search?keyword=test&page=101&page_size=100");
        request.Headers.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            IntegrationTestInternalAuth.Issue(
                SekaiInternalAuthDefaults.ApiServiceActor,
                SekaiInternalAuthDefaults.SearchServiceActor,
                SekaiInternalAuthDefaults.SearchQueryScope,
                activeContext.UserId,
                activeContext.TenantId));

        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        await AssertErrorResponseAsync(response);
        Assert.Empty(elasticsearch.SearchBodies);
    }

    /// <summary>
    /// Ensures page size values above the public contract are rejected.
    /// </summary>
    [Fact]
    public async Task InternalSearch_WhenPageSizeExceedsLimit_ReturnsBadRequest()
    {
        var activeContext = await GetActiveTenantContextAsync();
        using var client = searchFactory.CreateClient();
        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            "/internal/search?keyword=test&page=1&page_size=101");
        request.Headers.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            IntegrationTestInternalAuth.Issue(
                SekaiInternalAuthDefaults.ApiServiceActor,
                SekaiInternalAuthDefaults.SearchServiceActor,
                SekaiInternalAuthDefaults.SearchQueryScope,
                activeContext.UserId,
                activeContext.TenantId));

        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        await AssertErrorResponseAsync(response);
        Assert.Empty(elasticsearch.SearchBodies);
    }

    /// <summary>
    /// Ensures Search Service rejects unsupported internal actors.
    /// </summary>
    [Fact]
    public async Task InternalSearch_WithUnsupportedActor_ReturnsForbidden()
    {
        using var client = searchFactory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/internal/search?keyword=test");
        request.Headers.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            IntegrationTestInternalAuth.Issue(
                SekaiInternalAuthDefaults.AssetServiceActor,
                SekaiInternalAuthDefaults.SearchServiceActor,
                SekaiInternalAuthDefaults.SearchQueryScope,
                subjectUserId: 101,
                tenantId: 202));

        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Empty(elasticsearch.SearchBodies);
    }

    /// <summary>
    /// Verifies API Service proxies authenticated search requests with a scoped internal token.
    /// </summary>
    [Fact]
    public async Task ApiSearch_AuthenticatedTenantUser_ReturnsResultsAndIssuesSearchToken()
    {
        using var client = apiFactory.CreateClient();
        var login = await LoginAsync(
            client,
            IntegrationTestDatabaseFixture.AdminQqId,
            IntegrationTestDatabaseFixture.AdminPassword);

        using var response = await SendWithBearerAsync(
            client,
            HttpMethod.Get,
            "/api/search?keyword=%E3%81%93%E3%82%93&page=1&page_size=20",
            login.Token);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var json = await ReadJsonAsync(response);
        Assert.Equal(2, json.RootElement.GetProperty("items").GetArrayLength());
        Assert.Equal("source", json.RootElement.GetProperty("items")[0].GetProperty("asset_type").GetString());
        Assert.Equal("你好", json.RootElement.GetProperty("items")[1].GetProperty("highlight_text").GetString());
        var expectedUserId = login.Json.RootElement.GetProperty("user").GetProperty("id").GetInt64();
        var expectedTenantId = login.Json.RootElement
            .GetProperty("current_tenant")
            .GetProperty("id")
            .GetInt64();
        AssertSearchQueryToken(Assert.Single(searchServiceInternalTokens), expectedUserId, expectedTenantId);
    }

    /// <summary>
    /// Ensures a user without a selected tenant cannot call the public search API.
    /// </summary>
    [Fact]
    public async Task ApiSearch_WithoutSelectedTenant_ReturnsForbidden()
    {
        using var client = apiFactory.CreateClient();
        var login = await LoginAsync(
            client,
            IntegrationTestDatabaseFixture.MultiTenantUserQqId,
            IntegrationTestDatabaseFixture.MultiTenantUserPassword);

        using var response = await SendWithBearerAsync(
            client,
            HttpMethod.Get,
            "/api/search?keyword=test",
            login.Token);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    /// <summary>
    /// Ensures Search Service returns the platform error envelope for blank keywords.
    /// </summary>
    [Fact]
    public async Task ApiSearch_WithBlankKeyword_ReturnsBadRequest()
    {
        using var client = apiFactory.CreateClient();
        var login = await LoginAsync(
            client,
            IntegrationTestDatabaseFixture.AdminQqId,
            IntegrationTestDatabaseFixture.AdminPassword);

        using var response = await SendWithBearerAsync(
            client,
            HttpMethod.Get,
            "/api/search?keyword=%20",
            login.Token);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        await AssertErrorResponseAsync(response);
    }

    /// <summary>
    /// Disposes API, Auth, and Search hosts created for the test case.
    /// </summary>
    public void Dispose()
    {
        apiFactory.Dispose();
        searchFactory.Dispose();
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
    /// Sends an API request with bearer authentication.
    /// </summary>
    private static Task<HttpResponseMessage> SendWithBearerAsync(
        HttpClient client,
        HttpMethod method,
        string path,
        string token)
    {
        var request = new HttpRequestMessage(method, path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
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
    /// Verifies the Elasticsearch query body applies source sharing and tenant translation isolation.
    /// </summary>
    private static void AssertSearchBody(string body, long tenantId)
    {
        using var json = JsonDocument.Parse(body);
        var root = json.RootElement;
        Assert.Equal(5, root.GetProperty("from").GetInt64());
        Assert.Equal(5, root.GetProperty("size").GetInt32());
        Assert.Equal("html", root.GetProperty("highlight").GetProperty("encoder").GetString());

        var fields = root.GetProperty("query")
            .GetProperty("bool")
            .GetProperty("must")[0]
            .GetProperty("multi_match")
            .GetProperty("fields")
            .EnumerateArray()
            .Select(item => item.GetString()!)
            .ToArray();
        Assert.Equal(["text^3", "text.zh^2", "text.folded"], fields);

        var should = root.GetProperty("query")
            .GetProperty("bool")
            .GetProperty("filter")[0]
            .GetProperty("bool")
            .GetProperty("should");
        Assert.Equal("source", should[0]
            .GetProperty("term")
            .GetProperty("asset_type")
            .GetString());

        var translationFilters = should[1]
            .GetProperty("bool")
            .GetProperty("filter");
        Assert.Equal("translation", translationFilters[0]
            .GetProperty("term")
            .GetProperty("asset_type")
            .GetString());
        Assert.Equal(tenantId, translationFilters[1]
            .GetProperty("term")
            .GetProperty("tenant_id")
            .GetInt64());

        var sort = root.GetProperty("sort");
        Assert.Equal("source_line_id", sort[4].EnumerateObject().Single().Name);
        Assert.Equal("translation_version_id", sort[5].EnumerateObject().Single().Name);
    }

    /// <summary>
    /// Finds a seeded active user and tenant pair for delegated internal search calls.
    /// </summary>
    private async Task<ActiveTenantContext> GetActiveTenantContextAsync()
    {
        await using var dbContext = fixture.CreateDbContext();
        var item = await dbContext.UserTenants
            .Where(membership =>
                membership.Status == UserTenantStatuses.Active
                && membership.Tenant!.Name == IntegrationTestDatabaseFixture.TenantName)
            .Select(membership => new ActiveTenantContext(membership.UserId, membership.TenantId))
            .FirstAsync();
        return item;
    }

    /// <summary>
    /// Finds the primary seeded tenant identifier.
    /// </summary>
    private async Task<long> GetPrimaryTenantIdAsync()
    {
        await using var dbContext = fixture.CreateDbContext();
        return await dbContext.Tenants
            .Where(tenant => tenant.Name == IntegrationTestDatabaseFixture.TenantName)
            .Select(tenant => tenant.Id)
            .SingleAsync();
    }

    /// <summary>
    /// Seeds one source line with two current-tenant translations and one isolated other-tenant translation.
    /// </summary>
    private async Task<SearchPairSeed> SeedSearchPairAsync(ActiveTenantContext activeContext)
    {
        await using var dbContext = fixture.CreateDbContext();
        var now = DateTimeOffset.UtcNow;
        var unique = Guid.NewGuid().ToString("N");
        var otherTenantCreator = await dbContext.UserTenants
            .Where(membership =>
                membership.Tenant!.Name == IntegrationTestDatabaseFixture.SecondTenantName
                && membership.Status == UserTenantStatuses.Active)
            .Select(membership => new { membership.TenantId, membership.UserId })
            .FirstAsync();

        var group = new StoryGroup
        {
            StoryType = "event_story",
            ExternalType = "search_test",
            ExternalId = unique,
            Title = "搜索配对测试剧情集",
            CreatedAt = now,
            UpdatedAt = now
        };
        var story = new Story
        {
            Group = group,
            StoryType = "event_story",
            ScenarioId = $"search_pair_{unique}",
            Title = "搜索配对测试剧情",
            SortOrder = 1,
            CreatedAt = now,
            UpdatedAt = now
        };
        var sourceLine = new StorySourceLine
        {
            Story = story,
            LineNo = 1,
            LineType = "dialogue",
            Speaker = "ミク",
            Text = "原文配对测试",
            CreatedAt = now,
            UpdatedAt = now
        };
        var firstVersion = CreateTranslationVersion(activeContext.TenantId, story, activeContext.UserId, 1, "当前租户译文一", now);
        var secondVersion = CreateTranslationVersion(activeContext.TenantId, story, activeContext.UserId, 2, "当前租户译文二", now);
        var otherVersion = CreateTranslationVersion(
            otherTenantCreator.TenantId,
            story,
            otherTenantCreator.UserId,
            1,
            "其他租户译文",
            now);
        dbContext.StoryGroups.Add(group);
        dbContext.Stories.Add(story);
        dbContext.StorySourceLines.Add(sourceLine);
        dbContext.TranslationVersions.AddRange(firstVersion, secondVersion, otherVersion);
        await dbContext.SaveChangesAsync();

        var firstLine = CreateTranslationLine(firstVersion.Id, story.Id, sourceLine.Id, "初音未来", "译文配对测试一", now);
        var secondLine = CreateTranslationLine(secondVersion.Id, story.Id, sourceLine.Id, "初音未来", "译文配对测试二", now);
        dbContext.TranslationLines.AddRange(
            firstLine,
            secondLine,
            CreateTranslationLine(otherVersion.Id, story.Id, sourceLine.Id, "其他", "其他租户译文", now));
        await dbContext.SaveChangesAsync();

        return new SearchPairSeed(
            story.Id,
            group.Id,
            sourceLine.Id,
            firstVersion.Id,
            firstLine.Id);
    }

    /// <summary>
    /// Creates a translation version for search pairing tests.
    /// </summary>
    private static TranslationVersion CreateTranslationVersion(
        long tenantId,
        Story story,
        long createdBy,
        int versionNo,
        string title,
        DateTimeOffset now)
    {
        return new TranslationVersion
        {
            TenantId = tenantId,
            Story = story,
            VersionNo = versionNo,
            Title = title,
            CreatedBy = createdBy,
            CreatedAt = now,
            UpdatedAt = now
        };
    }

    /// <summary>
    /// Creates a translated line mapped to a source line for search pairing tests.
    /// </summary>
    private static TranslationLine CreateTranslationLine(
        long versionId,
        long storyId,
        long sourceLineId,
        string speaker,
        string text,
        DateTimeOffset now)
    {
        return new TranslationLine
        {
            VersionId = versionId,
            StoryId = storyId,
            SourceLineId = sourceLineId,
            LineNo = 1,
            Speaker = speaker,
            Text = text,
            CreatedAt = now,
            UpdatedAt = now
        };
    }

    /// <summary>
    /// Builds fake Elasticsearch hits whose identifiers match the seeded database rows.
    /// </summary>
    private static string CreateSearchPairResponse(SearchPairSeed seed, long tenantId)
    {
        return $$"""
        {
          "hits": {
            "total": { "value": 2, "relation": "eq" },
            "hits": [
              {
                "_id": "source:{{seed.SourceLineId}}",
                "_source": {
                  "asset_type": "source",
                  "tenant_id": null,
                  "story_id": {{seed.StoryId}},
                  "story_type": "event_story",
                  "story_title": "搜索配对测试剧情",
                  "story_group_id": {{seed.StoryGroupId}},
                  "story_group_title": "搜索配对测试剧情集",
                  "translation_version_id": null,
                  "source_line_id": {{seed.SourceLineId}},
                  "line_no": 1,
                  "speaker": "ミク",
                  "text": "原文配对测试"
                },
                "highlight": { "text": ["<mark>原文</mark>配对测试"] }
              },
              {
                "_id": "translation:{{seed.FirstTranslationLineId}}",
                "_source": {
                  "asset_type": "translation",
                  "tenant_id": {{tenantId}},
                  "story_id": {{seed.StoryId}},
                  "story_type": "event_story",
                  "story_title": "搜索配对测试剧情",
                  "story_group_id": {{seed.StoryGroupId}},
                  "story_group_title": "搜索配对测试剧情集",
                  "translation_version_id": {{seed.FirstTranslationVersionId}},
                  "source_line_id": {{seed.SourceLineId}},
                  "line_no": 1,
                  "speaker": "初音未来",
                  "text": "译文配对测试一"
                }
              }
            ]
          }
        }
        """;
    }

    /// <summary>
    /// Captures login credentials and payload needed by follow-up authenticated assertions.
    /// </summary>
    private sealed record LoginResult(string Token, JsonDocument Json);

    /// <summary>
    /// Captures an active user and tenant pair from the integration seed data.
    /// </summary>
    private sealed record ActiveTenantContext(long UserId, long TenantId);

    /// <summary>
    /// Captures seeded identifiers used by fake Elasticsearch search hits.
    /// </summary>
    private sealed record SearchPairSeed(
        long StoryId,
        long StoryGroupId,
        long SourceLineId,
        long FirstTranslationVersionId,
        long FirstTranslationLineId);

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
    /// Hosts Search Service with fake Elasticsearch and integration-test internal auth.
    /// </summary>
    private sealed class SearchServiceFactory(
        string connectionString,
        FakeElasticsearchSearchHandler elasticsearch) : WebApplicationFactory<SearchServiceProgram>
    {
        /// <summary>
        /// Injects deterministic configuration and replaces Elasticsearch HTTP calls.
        /// </summary>
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.ConfigureAppConfiguration((_, configuration) =>
            {
                var values = CreateConfiguration(connectionString, SekaiInternalAuthDefaults.SearchServiceActor);
                values["Elasticsearch:Url"] = "http://elasticsearch.test";
                values["Elasticsearch:IndexName"] = "sekai-language-assets-test";
                configuration.AddInMemoryCollection(values);
            });

            builder.ConfigureTestServices(services =>
            {
                services.AddHttpClient<ElasticsearchSearchClient>()
                    .ConfigurePrimaryHttpMessageHandler(() => elasticsearch);
            });
        }
    }

    /// <summary>
    /// Hosts the API service and routes internal Auth and Search clients to in-memory hosts.
    /// </summary>
    private sealed class ApiServiceFactory(
        string connectionString,
        AuthServiceFactory authFactory,
        SearchServiceFactory searchFactory,
        List<string?> searchServiceInternalTokens) : WebApplicationFactory<Program>
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
                services.AddHttpClient("search-service")
                    .ConfigurePrimaryHttpMessageHandler(() => new CapturingAuthorizationHandler(
                        searchFactory.Server.CreateHandler(),
                        searchServiceInternalTokens));
            });
        }
    }

    /// <summary>
    /// Captures Authorization tokens before forwarding requests to an in-memory service host.
    /// </summary>
    private sealed class CapturingAuthorizationHandler(
        HttpMessageHandler innerHandler,
        List<string?> tokens) : DelegatingHandler(innerHandler)
    {
        /// <summary>
        /// Records bearer tokens from proxied internal service calls.
        /// </summary>
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            tokens.Add(request.Headers.Authorization?.Parameter);
            return base.SendAsync(request, cancellationToken);
        }
    }

    /// <summary>
    /// Captures Elasticsearch search requests and returns deterministic hits.
    /// </summary>
    private sealed class FakeElasticsearchSearchHandler : HttpMessageHandler
    {
        /// <summary>
        /// Gets captured Elasticsearch search request bodies.
        /// </summary>
        public List<string> SearchBodies { get; } = [];

        /// <summary>
        /// Gets or sets the fake Elasticsearch search response body.
        /// </summary>
        public string SearchResponseJson { get; set; } = DefaultSearchResponseJson;

        /// <summary>
        /// Records search requests and returns successful search hits.
        /// </summary>
        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            if (request.RequestUri?.AbsolutePath.EndsWith("/_search", StringComparison.Ordinal) == true)
            {
                SearchBodies.Add(request.Content is null
                    ? ""
                    : await request.Content.ReadAsStringAsync(cancellationToken));
                return JsonResponse(SearchResponseJson);
            }

            return new HttpResponseMessage(HttpStatusCode.NotFound);
        }

        private const string DefaultSearchResponseJson = """
        {
          "hits": {
            "total": { "value": 2, "relation": "eq" },
            "hits": [
              {
                "_id": "source:401",
                "_source": {
                  "asset_type": "source",
                  "tenant_id": null,
                  "story_id": 301,
                  "story_type": "event_story",
                  "story_title": "第1話",
                  "story_group_id": 201,
                  "story_group_title": "テストイベント",
                  "translation_version_id": null,
                  "source_line_id": 401,
                  "line_no": 1,
                  "speaker": "ミク",
                  "text": "こんにちは"
                },
                "highlight": { "text": ["<mark>こんにちは</mark>"] }
              },
              {
                "_id": "translation:601",
                "_source": {
                  "asset_type": "translation",
                  "tenant_id": 202,
                  "story_id": 301,
                  "story_type": "event_story",
                  "story_title": "第1話",
                  "story_group_id": 201,
                  "story_group_title": "テストイベント",
                  "translation_version_id": 501,
                  "source_line_id": 401,
                  "line_no": 1,
                  "speaker": "初音未来",
                  "text": "你好"
                }
              }
            ]
          }
        }
        """;

        private static HttpResponseMessage JsonResponse(string json)
        {
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            };
        }
    }

    /// <summary>
    /// Creates shared service configuration used by API, Auth, and Search test hosts.
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

    private static void AssertSearchQueryToken(string? token, long expectedUserId, long expectedTenantId)
    {
        Assert.False(string.IsNullOrWhiteSpace(token));
        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(token);
        Assert.Equal(SekaiInternalAuthDefaults.ApiServiceActor, jwt.Claims.Single(claim =>
            claim.Type == SekaiInternalAuthDefaults.ActorClaimType).Value);
        Assert.Equal(SekaiInternalAuthDefaults.SearchQueryScope, jwt.Claims.Single(claim =>
            claim.Type == SekaiInternalAuthDefaults.ScopeClaimType).Value);
        Assert.Contains(SekaiInternalAuthDefaults.SearchServiceActor, jwt.Audiences);
        Assert.Equal(expectedTenantId.ToString(), jwt.Claims.Single(claim =>
            claim.Type == SekaiAuthDefaults.TenantIdClaimType).Value);
        Assert.Equal(expectedUserId.ToString(), jwt.Claims.Single(claim =>
            claim.Type == SekaiInternalAuthDefaults.SubjectUserIdClaimType).Value);
    }
}
