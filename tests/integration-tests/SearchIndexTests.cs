extern alias SearchService;

using System.Net;
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
using SekaiPlatform.Shared.Web;
using ElasticsearchIndexClient = SearchService::SekaiPlatform.SearchService.Search.ElasticsearchIndexClient;
using SearchServiceProgram = SearchService::Program;

namespace SekaiPlatform.IntegrationTests;

/// <summary>
/// Verifies Search Service index rebuild behavior against deterministic Elasticsearch responses.
/// </summary>
[Collection(IntegrationTestCollection.Name)]
public sealed class SearchIndexTests(IntegrationTestDatabaseFixture fixture)
{
    private const string MaintenanceToken = "integration-search-index-token";

    /// <summary>
    /// Rebuilds source documents without tenant ownership and includes story navigation fields.
    /// </summary>
    [Fact]
    public async Task RebuildSource_IndexesSharedSourceDocumentsWithoutTenantId()
    {
        await using var dbContext = fixture.CreateDbContext();
        var seed = await SeedSearchStoryAsync(dbContext, "search_source");
        var elasticsearch = new FakeElasticsearchHandler();
        await using var factory = new SearchServiceFactory(fixture.ConnectionString, elasticsearch);
        using var client = factory.CreateClient();

        using var response = await PostRebuildAsync(client, new
        {
            scope = "source",
            story_ids = new[] { seed.StoryId }
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var document = Assert.Single(elasticsearch.IndexedDocuments);
        Assert.Equal("source", document.GetProperty("asset_type").GetString());
        Assert.Equal(JsonValueKind.Null, document.GetProperty("tenant_id").ValueKind);
        Assert.Equal(seed.StoryId, document.GetProperty("story_id").GetInt64());
        Assert.Equal(seed.SourceLineId, document.GetProperty("source_line_id").GetInt64());
        Assert.Equal("こんにちは", document.GetProperty("text").GetString());
        Assert.Contains(elasticsearch.BulkActionIds, id => id == $"source:{seed.SourceLineId}");
    }

    /// <summary>
    /// Rebuilds translation documents with tenant and translation version ownership.
    /// </summary>
    [Fact]
    public async Task RebuildTranslation_IndexesTenantScopedTranslationDocuments()
    {
        await using var dbContext = fixture.CreateDbContext();
        var seed = await SeedSearchStoryAsync(dbContext, "search_translation");
        var elasticsearch = new FakeElasticsearchHandler();
        await using var factory = new SearchServiceFactory(fixture.ConnectionString, elasticsearch);
        using var client = factory.CreateClient();

        using var response = await PostRebuildAsync(client, new
        {
            scope = "translation",
            tenant_id = seed.TenantId,
            translation_version_id = seed.TranslationVersionId
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var document = Assert.Single(elasticsearch.IndexedDocuments);
        Assert.Equal("translation", document.GetProperty("asset_type").GetString());
        Assert.Equal(seed.TenantId, document.GetProperty("tenant_id").GetInt64());
        Assert.Equal(seed.TranslationVersionId, document.GetProperty("translation_version_id").GetInt64());
        Assert.Equal(seed.SourceLineId, document.GetProperty("source_line_id").GetInt64());
        Assert.Equal("你好", document.GetProperty("text").GetString());
        Assert.Contains(elasticsearch.BulkActionIds, id => id.StartsWith("translation:", StringComparison.Ordinal));
    }

    /// <summary>
    /// Ensures partial source rebuild deletes only matching source documents before bulk indexing.
    /// </summary>
    [Fact]
    public async Task RebuildSource_WithStoryIds_DeletesMatchingSourceDocuments()
    {
        await using var dbContext = fixture.CreateDbContext();
        var seed = await SeedSearchStoryAsync(dbContext, "search_delete_filter");
        var elasticsearch = new FakeElasticsearchHandler();
        await using var factory = new SearchServiceFactory(fixture.ConnectionString, elasticsearch);
        using var client = factory.CreateClient();

        using var response = await PostRebuildAsync(client, new
        {
            scope = "source",
            story_ids = new[] { seed.StoryId }
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var deleteBody = Assert.Single(elasticsearch.DeleteByQueryBodies);
        Assert.Contains("\"asset_type\":\"source\"", deleteBody, StringComparison.Ordinal);
        Assert.Contains(seed.StoryId.ToString(), deleteBody, StringComparison.Ordinal);
    }

    /// <summary>
    /// Ensures full all-scope story refresh deletes both source and translation documents for changed stories.
    /// </summary>
    [Fact]
    public async Task RebuildAll_WithStoryIds_RefreshesSourceAndTranslations()
    {
        await using var dbContext = fixture.CreateDbContext();
        var seed = await SeedSearchStoryAsync(dbContext, "search_all_filter");
        var elasticsearch = new FakeElasticsearchHandler();
        await using var factory = new SearchServiceFactory(fixture.ConnectionString, elasticsearch);
        using var client = factory.CreateClient();

        using var response = await PostRebuildAsync(client, new
        {
            scope = "all",
            story_ids = new[] { seed.StoryId }
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(2, elasticsearch.IndexedDocuments.Count);
        var deleteBody = Assert.Single(elasticsearch.DeleteByQueryBodies);
        Assert.DoesNotContain("\"asset_type\"", deleteBody, StringComparison.Ordinal);
        Assert.Contains(seed.StoryId.ToString(), deleteBody, StringComparison.Ordinal);
    }

    /// <summary>
    /// Rejects filtered physical index recreation because it would drop unrelated documents.
    /// </summary>
    [Fact]
    public async Task Rebuild_WithForceRecreateAndFilters_ReturnsBadRequest()
    {
        var elasticsearch = new FakeElasticsearchHandler();
        await using var factory = new SearchServiceFactory(fixture.ConnectionString, elasticsearch);
        using var client = factory.CreateClient();

        using var response = await PostRebuildAsync(client, new
        {
            scope = "source",
            force_recreate = true,
            story_ids = new[] { 1L }
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Empty(elasticsearch.DeleteIndexPaths);
    }

    /// <summary>
    /// Rejects source rebuild requests with tenant or translation-version filters.
    /// </summary>
    [Fact]
    public async Task RebuildSource_WithTenantFilter_ReturnsBadRequest()
    {
        var elasticsearch = new FakeElasticsearchHandler();
        await using var factory = new SearchServiceFactory(fixture.ConnectionString, elasticsearch);
        using var client = factory.CreateClient();

        using var response = await PostRebuildAsync(client, new
        {
            scope = "source",
            tenant_id = 1L
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Empty(elasticsearch.DeleteByQueryBodies);
    }

    /// <summary>
    /// Requires the internal maintenance token before mutating search index documents.
    /// </summary>
    [Fact]
    public async Task Rebuild_WithoutMaintenanceToken_ReturnsForbidden()
    {
        var elasticsearch = new FakeElasticsearchHandler();
        await using var factory = new SearchServiceFactory(fixture.ConnectionString, elasticsearch);
        using var client = factory.CreateClient();

        using var response = await client.PostAsJsonAsync("/internal/search/index/rebuild", new
        {
            scope = "source"
        });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Empty(elasticsearch.DeleteByQueryBodies);
    }

    /// <summary>
    /// Creates the Elasticsearch index mapping when the configured index is missing.
    /// </summary>
    [Fact]
    public async Task Rebuild_WhenIndexMissing_CreatesMappingWithLanguageAnalyzers()
    {
        await using var dbContext = fixture.CreateDbContext();
        await SeedSearchStoryAsync(dbContext, "search_mapping_create");
        var elasticsearch = new FakeElasticsearchHandler { IndexExists = false };
        await using var factory = new SearchServiceFactory(fixture.ConnectionString, elasticsearch);
        using var client = factory.CreateClient();

        using var response = await PostRebuildAsync(client, new { scope = "source" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var createBody = Assert.Single(elasticsearch.CreateIndexBodies);
        Assert.Contains("kuromoji_tokenizer", createBody, StringComparison.Ordinal);
        Assert.Contains("smartcn", createBody, StringComparison.Ordinal);
        Assert.Contains("icu_folding", createBody, StringComparison.Ordinal);
    }

    /// <summary>
    /// Seeds one story with one source line and one translation line for search index tests.
    /// </summary>
    private static async Task<SearchSeed> SeedSearchStoryAsync(
        SekaiPlatformDbContext dbContext,
        string scenarioPrefix)
    {
        var now = DateTimeOffset.UtcNow;
        var tenant = await dbContext.Tenants.SingleAsync(item => item.Name == IntegrationTestDatabaseFixture.TenantName);
        var admin = await dbContext.Users.SingleAsync(item => item.QqId == IntegrationTestDatabaseFixture.AdminQqId);
        var scenarioId = $"{scenarioPrefix}_{Guid.NewGuid():N}";
        var group = new StoryGroup
        {
            StoryType = "event_story",
            ExternalType = "event",
            ExternalId = scenarioId,
            Title = "搜索测试剧情集",
            CreatedAt = now,
            UpdatedAt = now
        };
        var story = new Story
        {
            Group = group,
            StoryType = "event_story",
            ScenarioId = scenarioId,
            Title = "搜索测试剧情",
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
            Text = "こんにちは",
            CreatedAt = now,
            UpdatedAt = now
        };
        var version = new TranslationVersion
        {
            TenantId = tenant.Id,
            Story = story,
            VersionNo = 1,
            Title = "中文译文",
            CreatedBy = admin.Id,
            CreatedAt = now,
            UpdatedAt = now
        };
        dbContext.StoryGroups.Add(group);
        dbContext.StorySourceLines.Add(sourceLine);
        dbContext.TranslationVersions.Add(version);
        await dbContext.SaveChangesAsync();

        var translationLine = new TranslationLine
        {
            VersionId = version.Id,
            SourceLineId = sourceLine.Id,
            StoryId = story.Id,
            LineNo = 1,
            Speaker = "初音未来",
            Text = "你好",
            CreatedAt = now,
            UpdatedAt = now
        };

        dbContext.TranslationLines.Add(translationLine);
        await dbContext.SaveChangesAsync();

        return new SearchSeed(
            tenant.Id,
            story.Id,
            sourceLine.Id,
            version.Id);
    }

    /// <summary>
    /// Hosts Search Service with fake Elasticsearch and the shared integration database.
    /// </summary>
    private sealed class SearchServiceFactory(
        string connectionString,
        FakeElasticsearchHandler elasticsearch) : WebApplicationFactory<SearchServiceProgram>
    {
        /// <summary>
        /// Injects deterministic configuration and replaces Elasticsearch HTTP calls.
        /// </summary>
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.ConfigureAppConfiguration((_, configuration) =>
            {
                configuration.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["ConnectionStrings:Postgres"] = connectionString,
                    ["Elasticsearch:Url"] = "http://elasticsearch.test",
                    ["Elasticsearch:IndexName"] = "sekai-language-assets-test",
                    ["Elasticsearch:BulkBatchSize"] = "1000",
                    ["SearchIndex:MaintenanceToken"] = MaintenanceToken,
                    ["Jwt:Issuer"] = "sekai-platform",
                    ["Jwt:Audience"] = "sekai-platform",
                    ["Jwt:SigningKey"] = "replace-with-local-development-signing-key"
                });
            });

            builder.ConfigureTestServices(services =>
            {
                services.AddHttpClient<ElasticsearchIndexClient>()
                    .ConfigurePrimaryHttpMessageHandler(() => elasticsearch);
            });
        }
    }

    /// <summary>
    /// Captures Elasticsearch maintenance and bulk requests for assertions.
    /// </summary>
    private sealed class FakeElasticsearchHandler : HttpMessageHandler
    {
        /// <summary>
        /// Gets delete-by-query request bodies.
        /// </summary>
        public List<string> DeleteByQueryBodies { get; } = [];

        /// <summary>
        /// Gets physical index delete paths.
        /// </summary>
        public List<string> DeleteIndexPaths { get; } = [];

        /// <summary>
        /// Gets create-index request bodies.
        /// </summary>
        public List<string> CreateIndexBodies { get; } = [];

        /// <summary>
        /// Gets bulk action document identifiers.
        /// </summary>
        public List<string> BulkActionIds { get; } = [];

        /// <summary>
        /// Gets indexed bulk documents.
        /// </summary>
        public List<JsonElement> IndexedDocuments { get; } = [];

        /// <summary>
        /// Gets or sets whether the fake index exists before rebuild starts.
        /// </summary>
        public bool IndexExists { get; set; } = true;

        /// <summary>
        /// Returns successful Elasticsearch maintenance responses and records request bodies.
        /// </summary>
        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            var path = request.RequestUri?.AbsolutePath ?? "";
            if (request.Method == HttpMethod.Head)
            {
                return new HttpResponseMessage(IndexExists ? HttpStatusCode.OK : HttpStatusCode.NotFound);
            }

            if (request.Method == HttpMethod.Put)
            {
                CreateIndexBodies.Add(request.Content is null
                    ? ""
                    : await request.Content.ReadAsStringAsync(cancellationToken));
                IndexExists = true;
                return JsonResponse("""{"acknowledged":true}""");
            }

            if (request.Method == HttpMethod.Delete)
            {
                DeleteIndexPaths.Add(path);
                IndexExists = false;
                return JsonResponse("""{"acknowledged":true}""");
            }

            if (path.EndsWith("/_delete_by_query", StringComparison.Ordinal))
            {
                DeleteByQueryBodies.Add(request.Content is null
                    ? ""
                    : await request.Content.ReadAsStringAsync(cancellationToken));
                return JsonResponse("""{"deleted":0}""");
            }

            if (path == "/_bulk")
            {
                var body = request.Content is null
                    ? ""
                    : await request.Content.ReadAsStringAsync(cancellationToken);
                CaptureBulkRequest(body);
                return JsonResponse("""{"errors":false,"items":[]}""");
            }

            return new HttpResponseMessage(HttpStatusCode.NotFound);
        }

        /// <summary>
        /// Captures every document line from Elasticsearch bulk NDJSON.
        /// </summary>
        private void CaptureBulkRequest(string body)
        {
            var lines = body.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            for (var index = 0; index < lines.Length; index += 2)
            {
                using var action = JsonDocument.Parse(lines[index]);
                BulkActionIds.Add(action.RootElement.GetProperty("index").GetProperty("_id").GetString()!);

                using var document = JsonDocument.Parse(lines[index + 1]);
                IndexedDocuments.Add(document.RootElement.Clone());
            }
        }

        private static HttpResponseMessage JsonResponse(string json)
        {
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            };
        }
    }

    /// <summary>
    /// Captures identifiers for seeded search index data.
    /// </summary>
    private sealed record SearchSeed(
        long TenantId,
        long StoryId,
        long SourceLineId,
        long TranslationVersionId);

    /// <summary>
    /// Sends an authorized search index rebuild request.
    /// </summary>
    private static Task<HttpResponseMessage> PostRebuildAsync(HttpClient client, object body)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/internal/search/index/rebuild")
        {
            Content = JsonContent.Create(body)
        };
        request.Headers.Add(SekaiHeaders.MaintenanceToken, MaintenanceToken);
        return client.SendAsync(request);
    }
}
