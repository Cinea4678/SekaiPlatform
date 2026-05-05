extern alias AssetService;
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
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SekaiPlatform.Database;
using SekaiPlatform.Shared.Web.Auth;
using SekaiPlatform.Shared.Web.Search;
using AssetServiceProgram = AssetService::Program;
using AuthServiceProgram = AuthService::Program;

namespace SekaiPlatform.IntegrationTests;

/// <summary>
/// Exercises Phase 7 translation import behavior through API, Auth, Asset, and search refresh paths.
/// </summary>
[Collection(IntegrationTestCollection.Name)]
public sealed class ImportApiTests : IDisposable
{
    private readonly IntegrationTestDatabaseFixture fixture;
    private readonly AuthServiceFactory authFactory;
    private readonly FakeSearchIndexHandler searchIndexHandler = new();
    private readonly AssetServiceFactory assetFactory;
    private readonly ApiServiceFactory apiFactory;

    /// <summary>
    /// Creates service hosts wired to the shared integration database and fake Search Service refresh endpoint.
    /// </summary>
    public ImportApiTests(IntegrationTestDatabaseFixture fixture)
    {
        this.fixture = fixture;
        authFactory = new AuthServiceFactory(fixture.ConnectionString);
        assetFactory = new AssetServiceFactory(fixture.ConnectionString, searchIndexHandler);
        apiFactory = new ApiServiceFactory(fixture.ConnectionString, authFactory, assetFactory);
    }

    /// <summary>
    /// Verifies an administrator can import multiple story translation versions in one request.
    /// </summary>
    [Fact]
    public async Task ImportTranslationVersions_AdminCreatesVersionsLinesAndRefreshesTranslationIndex()
    {
        var firstStory = await SeedStoryAsync("event_story", $"import_success_{Guid.NewGuid():N}", 2);
        var secondStory = await SeedStoryAsync("card_story", $"import_success_{Guid.NewGuid():N}", 1);
        using var client = apiFactory.CreateClient();
        var login = await LoginAsync(
            client,
            IntegrationTestDatabaseFixture.AdminQqId,
            IntegrationTestDatabaseFixture.AdminPassword);

        using var response = await SendWithBearerAsync(
            client,
            HttpMethod.Post,
            "/api/import/translation-versions",
            login.Token,
            new
            {
                items = new object[]
                {
                    new
                    {
                        story_type = firstStory.StoryType,
                        scenario_id = firstStory.ScenarioId,
                        title = "历史译文 A",
                        metadata = new
                        {
                            staff = new
                            {
                                translator = "翻译A",
                                proofreader = "校对B",
                                approver = "合意C"
                            },
                            source = "legacy-import"
                        },
                        lines = new object[]
                        {
                            new { line_no = 1, text = "你好一", metadata = new { source = "legacy" } },
                            new { line_no = 2, text = "你好二", speaker = "翻译说话人" }
                        }
                    },
                    new
                    {
                        story_type = secondStory.StoryType,
                        scenario_id = secondStory.ScenarioId,
                        title = "历史译文 B",
                        lines = new object[]
                        {
                            new { line_no = 1, text = "你好三" }
                        }
                    }
                }
            });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var json = await ReadJsonAsync(response);
        Assert.Equal(2, json.RootElement.GetProperty("total_versions").GetInt32());
        Assert.Equal(3, json.RootElement.GetProperty("total_lines").GetInt32());
        Assert.Equal(2, json.RootElement.GetProperty("items").GetArrayLength());

        await using var dbContext = fixture.CreateDbContext();
        var versionIds = json.RootElement.GetProperty("items")
            .EnumerateArray()
            .Select(item => item.GetProperty("translation_version_id").GetInt64())
            .ToArray();
        var versions = await dbContext.TranslationVersions
            .Where(version => versionIds.Contains(version.Id))
            .OrderBy(version => version.Id)
            .ToArrayAsync();
        Assert.Equal(2, versions.Length);
        Assert.All(versions, version => Assert.Equal(login.TenantId, version.TenantId));
        Assert.Contains(versions, version => version.StoryId == firstStory.StoryId && version.Title == "历史译文 A");
        Assert.Contains(versions, version => version.StoryId == secondStory.StoryId && version.Title == "历史译文 B");
        var firstVersion = versions.Single(version => version.StoryId == firstStory.StoryId);
        Assert.NotNull(firstVersion.Metadata);
        using (var metadata = JsonDocument.Parse(firstVersion.Metadata!))
        {
            var staff = metadata.RootElement.GetProperty("staff");
            Assert.Equal("翻译A", staff.GetProperty("translator").GetString());
            Assert.Equal("校对B", staff.GetProperty("proofreader").GetString());
            Assert.Equal("合意C", staff.GetProperty("approver").GetString());
            Assert.Equal("legacy-import", metadata.RootElement.GetProperty("source").GetString());
        }

        var lines = await dbContext.TranslationLines
            .Where(line => versionIds.Contains(line.VersionId))
            .OrderBy(line => line.LineNo)
            .ToArrayAsync();
        Assert.Equal(3, lines.Length);
        Assert.Contains(lines, line => line.StoryId == firstStory.StoryId && line.LineNo == 1 && line.Text == "你好一");
        Assert.Contains(lines, line => line.StoryId == firstStory.StoryId && line.LineNo == 2 && line.Speaker == "翻译说话人");
        Assert.Contains(lines, line => line.Metadata != null && line.Metadata.Contains("legacy", StringComparison.Ordinal));

        var body = Assert.Single(searchIndexHandler.RebuildBodies);
        using var rebuild = JsonDocument.Parse(body);
        Assert.Equal("translation", rebuild.RootElement.GetProperty("scope").GetString());
        Assert.Equal(login.TenantId, rebuild.RootElement.GetProperty("tenant_id").GetInt64());
        Assert.Equal(
            versionIds.Order().ToArray(),
            rebuild.RootElement.GetProperty("translation_version_ids")
                .EnumerateArray()
                .Select(item => item.GetInt64())
                .Order()
                .ToArray());

        using var versionRequest = new HttpRequestMessage(HttpMethod.Get, $"/api/translation-versions/{firstVersion.Id}");
        versionRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", login.Token);
        using var versionResponse = await client.SendAsync(versionRequest);
        Assert.Equal(HttpStatusCode.OK, versionResponse.StatusCode);
        var versionJson = await ReadJsonAsync(versionResponse);
        var responseStaff = versionJson.RootElement.GetProperty("metadata").GetProperty("staff");
        Assert.Equal("翻译A", responseStaff.GetProperty("translator").GetString());
        Assert.Equal("校对B", responseStaff.GetProperty("proofreader").GetString());
        Assert.Equal("合意C", responseStaff.GetProperty("approver").GetString());

        Assert.All(searchIndexHandler.InternalTokens, token => AssertSearchIndexRefreshToken(token, login.TenantId));
    }

    /// <summary>
    /// Verifies imported versions continue from the current tenant story maximum version number.
    /// </summary>
    [Fact]
    public async Task ImportTranslationVersions_WhenVersionsExist_CreatesNextVersionNumbers()
    {
        var story = await SeedStoryAsync("event_story", $"import_version_no_{Guid.NewGuid():N}", 2);
        await SeedTranslationVersionAsync(story.StoryId, versionNo: 1);
        using var client = apiFactory.CreateClient();
        var login = await LoginAsync(
            client,
            IntegrationTestDatabaseFixture.AdminQqId,
            IntegrationTestDatabaseFixture.AdminPassword);

        using var response = await SendWithBearerAsync(
            client,
            HttpMethod.Post,
            "/api/import/translation-versions",
            login.Token,
            new
            {
                items = new object[]
                {
                    CreateImportItem(story.StoryType, story.ScenarioId, 1),
                    CreateImportItem(story.StoryType, story.ScenarioId, 2)
                }
            });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var json = await ReadJsonAsync(response);
        Assert.Equal(
            new[] { 2, 3 },
            json.RootElement.GetProperty("items")
                .EnumerateArray()
                .Select(item => item.GetProperty("version_no").GetInt32())
                .Order()
                .ToArray());
    }

    /// <summary>
    /// Ensures concurrent imports for one tenant story serialize translation version numbers.
    /// </summary>
    [Fact]
    public async Task ImportTranslationVersions_WhenConcurrent_CreatesDistinctVersionNumbers()
    {
        var story = await SeedStoryAsync("event_story", $"import_concurrent_{Guid.NewGuid():N}", 1);
        using var client = apiFactory.CreateClient();
        var login = await LoginAsync(
            client,
            IntegrationTestDatabaseFixture.AdminQqId,
            IntegrationTestDatabaseFixture.AdminPassword);

        var first = SendWithBearerAsync(
            client,
            HttpMethod.Post,
            "/api/import/translation-versions",
            login.Token,
            CreateImportBody(story.StoryType, story.ScenarioId, 1));
        var second = SendWithBearerAsync(
            client,
            HttpMethod.Post,
            "/api/import/translation-versions",
            login.Token,
            CreateImportBody(story.StoryType, story.ScenarioId, 1));
        using var firstResponse = await first;
        using var secondResponse = await second;

        Assert.Equal(HttpStatusCode.OK, firstResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, secondResponse.StatusCode);
        await using var dbContext = fixture.CreateDbContext();
        Assert.Equal(
            new[] { 1, 2 },
            await dbContext.TranslationVersions
                .Where(version => version.StoryId == story.StoryId)
                .Select(version => version.VersionNo)
                .OrderBy(versionNo => versionNo)
                .ToArrayAsync());
    }

    /// <summary>
    /// Ensures normal tenant members cannot import historical translations.
    /// </summary>
    [Fact]
    public async Task ImportTranslationVersions_NormalUserIsForbidden()
    {
        var story = await SeedStoryAsync("event_story", $"import_forbidden_{Guid.NewGuid():N}", 1);
        using var client = apiFactory.CreateClient();
        var login = await LoginAsync(
            client,
            IntegrationTestDatabaseFixture.NormalUserQqId,
            IntegrationTestDatabaseFixture.NormalUserPassword);

        using var response = await SendWithBearerAsync(
            client,
            HttpMethod.Post,
            "/api/import/translation-versions",
            login.Token,
            CreateImportBody(story.StoryType, story.ScenarioId, 1));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        await AssertErrorResponseAsync(response);
        await using var dbContext = fixture.CreateDbContext();
        Assert.Equal(0, await dbContext.TranslationVersions.CountAsync(version => version.StoryId == story.StoryId));
        Assert.Empty(searchIndexHandler.RebuildBodies);
    }

    /// <summary>
    /// Ensures a bad line in a multi-story request rolls back the whole batch.
    /// </summary>
    [Fact]
    public async Task ImportTranslationVersions_WhenOneItemInvalid_RollsBackWholeBatch()
    {
        var validStory = await SeedStoryAsync("event_story", $"import_rollback_valid_{Guid.NewGuid():N}", 1);
        var invalidStory = await SeedStoryAsync("event_story", $"import_rollback_invalid_{Guid.NewGuid():N}", 1);
        using var client = apiFactory.CreateClient();
        var login = await LoginAsync(
            client,
            IntegrationTestDatabaseFixture.AdminQqId,
            IntegrationTestDatabaseFixture.AdminPassword);

        using var response = await SendWithBearerAsync(
            client,
            HttpMethod.Post,
            "/api/import/translation-versions",
            login.Token,
            new
            {
                items = new object[]
                {
                    CreateImportItem(validStory.StoryType, validStory.ScenarioId, 1),
                    CreateImportItem(invalidStory.StoryType, invalidStory.ScenarioId, 999)
                }
            });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        await AssertErrorResponseAsync(response);
        await using var dbContext = fixture.CreateDbContext();
        Assert.Equal(0, await dbContext.TranslationVersions.CountAsync(version =>
            version.StoryId == validStory.StoryId || version.StoryId == invalidStory.StoryId));
        Assert.Empty(searchIndexHandler.RebuildBodies);
    }

    /// <summary>
    /// Ensures the import endpoint reports malformed JSON with the platform error envelope.
    /// </summary>
    [Fact]
    public async Task ImportTranslationVersions_WithMalformedJson_ReturnsBadRequest()
    {
        using var client = apiFactory.CreateClient();
        var login = await LoginAsync(
            client,
            IntegrationTestDatabaseFixture.AdminQqId,
            IntegrationTestDatabaseFixture.AdminPassword);
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/import/translation-versions")
        {
            Content = new StringContent("{", Encoding.UTF8, "application/json")
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", login.Token);

        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        await AssertErrorResponseAsync(response);
        Assert.Empty(searchIndexHandler.RebuildBodies);
    }

    /// <summary>
    /// Ensures malformed import fields are rejected before any database write.
    /// </summary>
    [Fact]
    public async Task ImportTranslationVersions_WithInvalidFields_ReturnsBadRequestAndDoesNotWrite()
    {
        var story = await SeedStoryAsync("event_story", $"import_invalid_fields_{Guid.NewGuid():N}", 1);
        using var client = apiFactory.CreateClient();
        var login = await LoginAsync(
            client,
            IntegrationTestDatabaseFixture.AdminQqId,
            IntegrationTestDatabaseFixture.AdminPassword);

        foreach (var body in new object[]
        {
            new { items = Array.Empty<object>() },
            new { items = new[] { new { story_type = story.StoryType, scenario_id = story.ScenarioId, lines = Array.Empty<object>() } } },
            new { items = new[] { new { story_type = story.StoryType, scenario_id = story.ScenarioId, lines = new[] { new { line_no = 1, text = "a" }, new { line_no = 1, text = "b" } } } } },
            new { items = new[] { new { story_type = story.StoryType, scenario_id = story.ScenarioId, lines = new[] { new { line_no = 1, text = " " } } } } },
            new { items = new[] { new { story_type = story.StoryType, scenario_id = story.ScenarioId, title = new string('a', 256), lines = new[] { new { line_no = 1, text = "a" } } } } },
            new { items = new[] { new { story_type = story.StoryType, scenario_id = story.ScenarioId, lines = new[] { new { line_no = 1, text = "a", speaker = new string('a', 129) } } } } },
            new { items = new[] { new { story_type = story.StoryType, scenario_id = story.ScenarioId, lines = new[] { new { line_no = 1, text = "a", metadata = new[] { "bad" } } } } } }
        })
        {
            using var response = await SendWithBearerAsync(
                client,
                HttpMethod.Post,
                "/api/import/translation-versions",
                login.Token,
                body);

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            await AssertErrorResponseAsync(response);
        }

        await using var dbContext = fixture.CreateDbContext();
        Assert.Equal(0, await dbContext.TranslationVersions.CountAsync(version => version.StoryId == story.StoryId));
    }

    /// <summary>
    /// Ensures malformed version metadata is rejected before any database write.
    /// </summary>
    [Fact]
    public async Task ImportTranslationVersions_WithInvalidVersionMetadata_ReturnsBadRequestAndDoesNotWrite()
    {
        var story = await SeedStoryAsync("event_story", $"import_invalid_version_metadata_{Guid.NewGuid():N}", 1);
        using var client = apiFactory.CreateClient();
        var login = await LoginAsync(
            client,
            IntegrationTestDatabaseFixture.AdminQqId,
            IntegrationTestDatabaseFixture.AdminPassword);

        foreach (var body in new object[]
        {
            new { items = new[] { new { story_type = story.StoryType, scenario_id = story.ScenarioId, metadata = new[] { "bad" }, lines = new[] { new { line_no = 1, text = "a" } } } } },
            new { items = new[] { new { story_type = story.StoryType, scenario_id = story.ScenarioId, metadata = new { staff = new[] { "bad" } }, lines = new[] { new { line_no = 1, text = "a" } } } } },
            new { items = new[] { new { story_type = story.StoryType, scenario_id = story.ScenarioId, metadata = new { staff = new { translator = new { name = "bad" } } }, lines = new[] { new { line_no = 1, text = "a" } } } } },
            new { items = new[] { new { story_type = story.StoryType, scenario_id = story.ScenarioId, metadata = new { staff = new { translator = new string('a', 20000) } }, lines = new[] { new { line_no = 1, text = "a" } } } } }
        })
        {
            using var response = await SendWithBearerAsync(
                client,
                HttpMethod.Post,
                "/api/import/translation-versions",
                login.Token,
                body);

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            await AssertErrorResponseAsync(response);
        }

        await using var dbContext = fixture.CreateDbContext();
        Assert.Equal(0, await dbContext.TranslationVersions.CountAsync(version => version.StoryId == story.StoryId));
    }

    /// <summary>
    /// Ensures an unknown story key returns not found without writing any import data.
    /// </summary>
    [Fact]
    public async Task ImportTranslationVersions_WithUnknownStory_ReturnsNotFound()
    {
        using var client = apiFactory.CreateClient();
        var login = await LoginAsync(
            client,
            IntegrationTestDatabaseFixture.AdminQqId,
            IntegrationTestDatabaseFixture.AdminPassword);

        using var response = await SendWithBearerAsync(
            client,
            HttpMethod.Post,
            "/api/import/translation-versions",
            login.Token,
            CreateImportBody("event_story", $"missing_{Guid.NewGuid():N}", 1));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        await AssertErrorResponseAsync(response);
        Assert.Empty(searchIndexHandler.RebuildBodies);
    }

    /// <summary>
    /// Ensures invalid content type syntax is handled as a bad JSON import request.
    /// </summary>
    [Fact]
    public async Task ImportTranslationVersions_WithInvalidContentType_ReturnsBadRequest()
    {
        using var client = apiFactory.CreateClient();
        var login = await LoginAsync(
            client,
            IntegrationTestDatabaseFixture.AdminQqId,
            IntegrationTestDatabaseFixture.AdminPassword);
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/import/translation-versions")
        {
            Content = new StringContent("""{"items":[]}""", Encoding.UTF8, "application/json")
        };
        request.Content.Headers.Remove("Content-Type");
        request.Content.Headers.TryAddWithoutValidation("Content-Type", "bad content type");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", login.Token);

        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        await AssertErrorResponseAsync(response);
    }

    /// <summary>
    /// Verifies API Service signs import proxy calls with the expected internal token scope and context.
    /// </summary>
    [Fact]
    public async Task ApiImport_AuthenticatedUserIssuesImportToken()
    {
        using var fakeAsset = new FakeAssetServiceHandler();
        await using var apiFactoryWithFakeAsset = new ApiServiceFactory(
            fixture.ConnectionString,
            authFactory,
            fakeAsset);
        using var client = apiFactoryWithFakeAsset.CreateClient();
        var login = await LoginAsync(
            client,
            IntegrationTestDatabaseFixture.AdminQqId,
            IntegrationTestDatabaseFixture.AdminPassword);

        using var response = await SendWithBearerAsync(
            client,
            HttpMethod.Post,
            "/api/import/translation-versions",
            login.Token,
            new { items = Array.Empty<object>() });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var token = Assert.Single(fakeAsset.InternalTokens);
        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(token);
        Assert.Equal(SekaiInternalAuthDefaults.ApiServiceActor, jwt.Claims.Single(claim =>
            claim.Type == SekaiInternalAuthDefaults.ActorClaimType).Value);
        Assert.Equal(SekaiInternalAuthDefaults.TranslationsImportWriteScope, jwt.Claims.Single(claim =>
            claim.Type == SekaiInternalAuthDefaults.ScopeClaimType).Value);
        Assert.Equal(login.UserId.ToString(), jwt.Claims.Single(claim =>
            claim.Type == SekaiInternalAuthDefaults.SubjectUserIdClaimType).Value);
        Assert.Equal(login.TenantId.ToString(), jwt.Claims.Single(claim =>
            claim.Type == SekaiAuthDefaults.TenantIdClaimType).Value);
        Assert.Contains(SekaiInternalAuthDefaults.AssetServiceActor, jwt.Audiences);
    }

    /// <summary>
    /// Verifies import write rate limiting is partitioned by the client IP from X-Forwarded-For.
    /// </summary>
    [Fact]
    public async Task ApiImportRateLimit_UsesForwardedForIpPartitions()
    {
        using var fakeAsset = new FakeAssetServiceHandler();
        await using var apiFactoryWithFakeAsset = new ApiServiceFactory(
            fixture.ConnectionString,
            authFactory,
            fakeAsset);
        using var client = apiFactoryWithFakeAsset.CreateClient();
        var login = await LoginAsync(
            client,
            IntegrationTestDatabaseFixture.AdminQqId,
            IntegrationTestDatabaseFixture.AdminPassword);

        for (var i = 0; i < 60; i++)
        {
            using var response = await SendImportWithForwardedForAsync(
                client,
                login.Token,
                "203.0.113.10, 10.0.0.1");
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        using var limitedResponse = await SendImportWithForwardedForAsync(
            client,
            login.Token,
            "203.0.113.10, 10.0.0.1");
        Assert.Equal(HttpStatusCode.TooManyRequests, limitedResponse.StatusCode);

        using var otherIpResponse = await SendImportWithForwardedForAsync(
            client,
            login.Token,
            "203.0.113.11");
        Assert.Equal(HttpStatusCode.OK, otherIpResponse.StatusCode);
        Assert.Equal(61, fakeAsset.InternalTokens.Count);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        authFactory.Dispose();
        assetFactory.Dispose();
        apiFactory.Dispose();
    }

    /// <summary>
    /// Seeds a story with source lines for translation import tests.
    /// </summary>
    private async Task<SeededStory> SeedStoryAsync(string storyType, string scenarioId, int lineCount)
    {
        await using var dbContext = fixture.CreateDbContext();
        var now = DateTimeOffset.UtcNow;
        var story = new Story
        {
            StoryType = storyType,
            ScenarioId = scenarioId,
            Title = $"导入测试 {scenarioId}",
            SortOrder = 1,
            CreatedAt = now,
            UpdatedAt = now
        };
        dbContext.Stories.Add(story);
        await dbContext.SaveChangesAsync();

        for (var lineNo = 1; lineNo <= lineCount; lineNo++)
        {
            dbContext.StorySourceLines.Add(new StorySourceLine
            {
                StoryId = story.Id,
                LineNo = lineNo,
                LineType = "dialogue",
                Speaker = $"原文说话人 {lineNo}",
                Text = $"原文 {lineNo}",
                CreatedAt = now,
                UpdatedAt = now
            });
        }

        await dbContext.SaveChangesAsync();
        return new SeededStory(story.Id, story.StoryType, story.ScenarioId);
    }

    /// <summary>
    /// Seeds an existing translation version for version number allocation tests.
    /// </summary>
    private async Task SeedTranslationVersionAsync(long storyId, int versionNo)
    {
        await using var dbContext = fixture.CreateDbContext();
        var tenant = await dbContext.Tenants.SingleAsync(item => item.Name == IntegrationTestDatabaseFixture.TenantName);
        var admin = await dbContext.Users.SingleAsync(item => item.QqId == IntegrationTestDatabaseFixture.AdminQqId);
        var now = DateTimeOffset.UtcNow;
        dbContext.TranslationVersions.Add(new TranslationVersion
        {
            TenantId = tenant.Id,
            StoryId = storyId,
            VersionNo = versionNo,
            Title = "已有译文",
            CreatedBy = admin.Id,
            CreatedAt = now,
            UpdatedAt = now
        });
        await dbContext.SaveChangesAsync();
    }

    /// <summary>
    /// Creates a single-item import request body.
    /// </summary>
    private static object CreateImportBody(string storyType, string scenarioId, int lineNo)
    {
        return new { items = new[] { CreateImportItem(storyType, scenarioId, lineNo) } };
    }

    /// <summary>
    /// Creates one import item with a single translated line.
    /// </summary>
    private static object CreateImportItem(string storyType, string scenarioId, int lineNo)
    {
        return new
        {
            story_type = storyType,
            scenario_id = scenarioId,
            title = "历史译文",
            lines = new[] { new { line_no = lineNo, text = "导入译文" } }
        };
    }

    /// <summary>
    /// Logs in and returns the access token plus user and tenant identifiers.
    /// </summary>
    private static async Task<LoginResult> LoginAsync(HttpClient client, string username, string password)
    {
        using var response = await client.PostAsJsonAsync("/api/auth/login", new
        {
            username,
            password
        });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var json = await ReadJsonAsync(response);
        return new LoginResult(
            json.RootElement.GetProperty("access_token").GetString()!,
            json.RootElement.GetProperty("user").GetProperty("id").GetInt64(),
            json.RootElement.GetProperty("current_tenant").GetProperty("id").GetInt64());
    }

    /// <summary>
    /// Sends an authenticated JSON request to API Service.
    /// </summary>
    private static async Task<HttpResponseMessage> SendWithBearerAsync(
        HttpClient client,
        HttpMethod method,
        string path,
        string token,
        object body)
    {
        var request = new HttpRequestMessage(method, path)
        {
            Content = JsonContent.Create(body)
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return await client.SendAsync(request);
    }

    /// <summary>
    /// Sends an authenticated import request with an X-Forwarded-For client IP chain.
    /// </summary>
    private static async Task<HttpResponseMessage> SendImportWithForwardedForAsync(
        HttpClient client,
        string token,
        string forwardedFor)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/import/translation-versions")
        {
            Content = JsonContent.Create(new { items = Array.Empty<object>() })
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        request.Headers.TryAddWithoutValidation("X-Forwarded-For", forwardedFor);
        return await client.SendAsync(request);
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
    /// Verifies that Asset Service requested a search index rebuild using its internal actor.
    /// </summary>
    private static void AssertSearchIndexRefreshToken(string? token, long tenantId)
    {
        Assert.False(string.IsNullOrWhiteSpace(token));
        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(token);
        Assert.Equal(SekaiInternalAuthDefaults.AssetServiceActor, jwt.Claims.Single(claim =>
            claim.Type == SekaiInternalAuthDefaults.ActorClaimType).Value);
        Assert.Equal(SekaiInternalAuthDefaults.SearchTranslationRefreshScope, jwt.Claims.Single(claim =>
            claim.Type == SekaiInternalAuthDefaults.ScopeClaimType).Value);
        Assert.Equal(tenantId.ToString(), jwt.Claims.Single(claim =>
            claim.Type == SekaiAuthDefaults.TenantIdClaimType).Value);
        Assert.Contains(SekaiInternalAuthDefaults.SearchServiceActor, jwt.Audiences);
    }

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
    /// Hosts the Asset service with fake Search Service refresh responses.
    /// </summary>
    private sealed class AssetServiceFactory(
        string connectionString,
        FakeSearchIndexHandler searchIndexHandler) : WebApplicationFactory<AssetServiceProgram>
    {
        /// <summary>
        /// Injects configuration and replaces the Search Service HTTP client.
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
                services.AddHttpClient<SearchIndexRefreshClient>()
                    .ConfigurePrimaryHttpMessageHandler(() => searchIndexHandler);
            });
        }
    }

    /// <summary>
    /// Hosts API Service and routes internal Auth and Asset clients to in-memory handlers.
    /// </summary>
    private sealed class ApiServiceFactory : WebApplicationFactory<Program>
    {
        private readonly string connectionString;
        private readonly AuthServiceFactory authFactory;
        private readonly AssetServiceFactory? assetFactory;
        private readonly HttpMessageHandler? assetHandler;

        /// <summary>
        /// Creates an API host that routes Asset calls to a real in-memory Asset Service.
        /// </summary>
        public ApiServiceFactory(
            string connectionString,
            AuthServiceFactory authFactory,
            AssetServiceFactory assetFactory)
        {
            this.connectionString = connectionString;
            this.authFactory = authFactory;
            this.assetFactory = assetFactory;
        }

        /// <summary>
        /// Creates an API host that routes Asset calls to a fake HTTP handler.
        /// </summary>
        public ApiServiceFactory(
            string connectionString,
            AuthServiceFactory authFactory,
            HttpMessageHandler assetHandler)
        {
            this.connectionString = connectionString;
            this.authFactory = authFactory;
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
                    SekaiInternalAuthDefaults.ApiServiceActor,
                    includePrivateKey: true));
            });

            builder.ConfigureTestServices(services =>
            {
                services.AddHttpClient("auth-service")
                    .ConfigurePrimaryHttpMessageHandler(() => authFactory.Server.CreateHandler());
                services.AddHttpClient("asset-service")
                    .ConfigurePrimaryHttpMessageHandler(() =>
                        assetHandler ?? assetFactory!.Server.CreateHandler());
            });
        }
    }

    /// <summary>
    /// Captures search index refresh calls made by Asset Service after successful imports.
    /// </summary>
    private sealed class FakeSearchIndexHandler : HttpMessageHandler
    {
        private readonly object gate = new();

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
                var body = request.Content is null
                    ? ""
                    : await request.Content.ReadAsStringAsync(cancellationToken);
                lock (gate)
                {
                    InternalTokens.Add(request.Headers.Authorization?.Parameter);
                    RebuildBodies.Add(body);
                }

                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(
                        """{"scope":"translation","deleted":true,"source_indexed":0,"translation_indexed":1}""",
                        Encoding.UTF8,
                        "application/json")
                };
            }

            return new HttpResponseMessage(HttpStatusCode.NotFound);
        }
    }

    /// <summary>
    /// Captures API Service calls to Asset Service for proxy-token assertions.
    /// </summary>
    private sealed class FakeAssetServiceHandler : HttpMessageHandler
    {
        /// <summary>
        /// Gets captured internal bearer tokens sent by API Service.
        /// </summary>
        public List<string?> InternalTokens { get; } = [];

        /// <summary>
        /// Records import requests and returns a successful empty response.
        /// </summary>
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            if (request.RequestUri?.AbsolutePath == "/internal/import/translation-versions")
            {
                InternalTokens.Add(request.Headers.Authorization?.Parameter);
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(
                        """{"items":[],"total_versions":0,"total_lines":0}""",
                        Encoding.UTF8,
                        "application/json")
                });
            }

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
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

    /// <summary>
    /// Captures login credentials and identifiers needed by follow-up authenticated assertions.
    /// </summary>
    private sealed record LoginResult(string Token, long UserId, long TenantId);

    /// <summary>
    /// Captures seeded story identifiers used by import requests.
    /// </summary>
    private sealed record SeededStory(long StoryId, string StoryType, string ScenarioId);
}
