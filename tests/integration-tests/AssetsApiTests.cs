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
using AuthServiceProgram = AuthService::Program;
using AssetServiceProgram = AssetService::Program;

namespace SekaiPlatform.IntegrationTests;

/// <summary>
/// Exercises asset read behavior through API, Auth, Asset, and PostgreSQL paths.
/// </summary>
[Collection(IntegrationTestCollection.Name)]
public sealed class AssetsApiTests : IDisposable
{
    private readonly IntegrationTestDatabaseFixture fixture;
    private readonly AuthServiceFactory authFactory;
    private readonly AssetServiceFactory assetFactory;
    private readonly ApiServiceFactory apiFactory;

    /// <summary>
    /// Creates service hosts wired to the shared integration database.
    /// </summary>
    public AssetsApiTests(IntegrationTestDatabaseFixture fixture)
    {
        this.fixture = fixture;
        authFactory = new AuthServiceFactory(fixture.ConnectionString);
        assetFactory = new AssetServiceFactory(fixture.ConnectionString);
        apiFactory = new ApiServiceFactory(fixture.ConnectionString, authFactory, assetFactory);
    }

    /// <summary>
    /// Verifies a tenant member can browse story types, groups, stories, and source lines.
    /// </summary>
    [Fact]
    public async Task AssetsReadEndpoints_ReturnNavigationStoryAndSourceLines()
    {
        var seed = await SeedStoryAsync("event_story", "asset_nav", "资源导航剧情集", 2);
        using var client = apiFactory.CreateClient();
        var login = await LoginAsync(
            client,
            IntegrationTestDatabaseFixture.NormalUserQqId,
            IntegrationTestDatabaseFixture.NormalUserPassword);

        using var storyTypesResponse = await SendWithBearerAsync(client, "/api/story-types", login.Token);
        Assert.Equal(HttpStatusCode.OK, storyTypesResponse.StatusCode);
        var storyTypes = await ReadJsonAsync(storyTypesResponse);
        Assert.Contains(storyTypes.RootElement.EnumerateArray(), item =>
            item.GetProperty("value").GetString() == "event_story"
            && item.GetProperty("label").GetString() == "活动剧情");

        using var groupsResponse = await SendWithBearerAsync(
            client,
            $"/api/story-groups?story_type=event_story&keyword={seed.ExternalId}&page=1&page_size=10",
            login.Token);
        Assert.Equal(HttpStatusCode.OK, groupsResponse.StatusCode);
        var groups = await ReadJsonAsync(groupsResponse);
        Assert.True(groups.RootElement.GetProperty("page").GetProperty("total").GetInt32() >= 1);
        var group = groups.RootElement.GetProperty("items").EnumerateArray()
            .Single(item => item.GetProperty("id").GetInt64() == seed.GroupId);
        Assert.Equal("资源导航剧情集", group.GetProperty("title").GetString());
        Assert.Equal("asset-read", group.GetProperty("metadata").GetProperty("kind").GetString());

        using var groupResponse = await SendWithBearerAsync(client, $"/api/story-groups/{seed.GroupId}", login.Token);
        Assert.Equal(HttpStatusCode.OK, groupResponse.StatusCode);
        var groupDetail = await ReadJsonAsync(groupResponse);
        Assert.Equal(seed.GroupId, groupDetail.RootElement.GetProperty("id").GetInt64());

        using var storiesResponse = await SendWithBearerAsync(
            client,
            $"/api/stories?story_group_id={seed.GroupId}&keyword={seed.ScenarioId}",
            login.Token);
        Assert.Equal(HttpStatusCode.OK, storiesResponse.StatusCode);
        var stories = await ReadJsonAsync(storiesResponse);
        var story = Assert.Single(stories.RootElement.GetProperty("items").EnumerateArray());
        Assert.Equal(seed.StoryId, story.GetProperty("id").GetInt64());
        Assert.Equal(seed.GroupId, story.GetProperty("group").GetProperty("id").GetInt64());

        using var storyResponse = await SendWithBearerAsync(client, $"/api/stories/{seed.StoryId}", login.Token);
        Assert.Equal(HttpStatusCode.OK, storyResponse.StatusCode);
        var storyDetail = await ReadJsonAsync(storyResponse);
        Assert.Equal(seed.ScenarioId, storyDetail.RootElement.GetProperty("scenario_id").GetString());

        using var linesResponse = await SendWithBearerAsync(client, $"/api/stories/{seed.StoryId}/source-lines", login.Token);
        Assert.Equal(HttpStatusCode.OK, linesResponse.StatusCode);
        var lines = await ReadJsonAsync(linesResponse);
        Assert.Equal(new[] { 1, 2 }, lines.RootElement.EnumerateArray()
            .Select(item => item.GetProperty("line_no").GetInt32())
            .ToArray());
        Assert.Equal("source", lines.RootElement[0].GetProperty("metadata").GetProperty("kind").GetString());
    }

    /// <summary>
    /// Ensures translation reads are scoped to the current tenant and keep line order.
    /// </summary>
    [Fact]
    public async Task TranslationReadEndpoints_ReturnOnlyCurrentTenantVersionsAndLines()
    {
        var seed = await SeedStoryAsync("card_story", "translation_read", "译文隔离剧情集", 2);
        var versions = await SeedTenantTranslationsAsync(seed.StoryId, seed.SourceLineIds);
        using var client = apiFactory.CreateClient();
        var login = await LoginAsync(
            client,
            IntegrationTestDatabaseFixture.NormalUserQqId,
            IntegrationTestDatabaseFixture.NormalUserPassword);

        using var versionsResponse = await SendWithBearerAsync(
            client,
            $"/api/stories/{seed.StoryId}/translation-versions",
            login.Token);
        Assert.Equal(HttpStatusCode.OK, versionsResponse.StatusCode);
        var versionPage = await ReadJsonAsync(versionsResponse);
        Assert.Equal(1, versionPage.RootElement.GetProperty("page").GetProperty("total").GetInt32());
        var version = Assert.Single(versionPage.RootElement.GetProperty("items").EnumerateArray());
        Assert.Equal(versions.PrimaryVersionId, version.GetProperty("id").GetInt64());
        Assert.Equal("当前租户译文", version.GetProperty("title").GetString());
        var versionStaff = version.GetProperty("metadata").GetProperty("staff");
        Assert.Equal("翻译A", versionStaff.GetProperty("translator").GetString());
        Assert.Equal("校对B", versionStaff.GetProperty("proofreader").GetString());
        Assert.Equal("合意C", versionStaff.GetProperty("approver").GetString());

        using var versionResponse = await SendWithBearerAsync(
            client,
            $"/api/translation-versions/{versions.PrimaryVersionId}",
            login.Token);
        Assert.Equal(HttpStatusCode.OK, versionResponse.StatusCode);
        var versionDetail = await ReadJsonAsync(versionResponse);
        Assert.Equal(1, versionDetail.RootElement.GetProperty("version_no").GetInt32());
        var detailStaff = versionDetail.RootElement.GetProperty("metadata").GetProperty("staff");
        Assert.Equal("翻译A", detailStaff.GetProperty("translator").GetString());
        Assert.Equal("校对B", detailStaff.GetProperty("proofreader").GetString());
        Assert.Equal("合意C", detailStaff.GetProperty("approver").GetString());

        using var linesResponse = await SendWithBearerAsync(
            client,
            $"/api/translation-versions/{versions.PrimaryVersionId}/lines",
            login.Token);
        Assert.Equal(HttpStatusCode.OK, linesResponse.StatusCode);
        var lines = await ReadJsonAsync(linesResponse);
        Assert.Equal(new[] { 1, 2 }, lines.RootElement.EnumerateArray()
            .Select(item => item.GetProperty("line_no").GetInt32())
            .ToArray());
        Assert.Equal("translation", lines.RootElement[0].GetProperty("metadata").GetProperty("kind").GetString());

        using var crossTenantResponse = await SendWithBearerAsync(
            client,
            $"/api/translation-versions/{versions.OtherTenantVersionId}",
            login.Token);
        Assert.Equal(HttpStatusCode.NotFound, crossTenantResponse.StatusCode);
        await AssertErrorResponseAsync(crossTenantResponse);

        using var crossTenantLinesResponse = await SendWithBearerAsync(
            client,
            $"/api/translation-versions/{versions.OtherTenantVersionId}/lines",
            login.Token);
        Assert.Equal(HttpStatusCode.NotFound, crossTenantLinesResponse.StatusCode);
        await AssertErrorResponseAsync(crossTenantLinesResponse);

        var secondTenantToken = await LoginAndSwitchTenantAsync(
            client,
            IntegrationTestDatabaseFixture.MultiTenantUserQqId,
            IntegrationTestDatabaseFixture.MultiTenantUserPassword,
            versions.OtherTenantId);
        using var secondTenantVersionsResponse = await SendWithBearerAsync(
            client,
            $"/api/stories/{seed.StoryId}/translation-versions",
            secondTenantToken);
        Assert.Equal(HttpStatusCode.OK, secondTenantVersionsResponse.StatusCode);
        var secondTenantVersionPage = await ReadJsonAsync(secondTenantVersionsResponse);
        var secondTenantVersion = Assert.Single(secondTenantVersionPage.RootElement.GetProperty("items").EnumerateArray());
        Assert.Equal(versions.OtherTenantVersionId, secondTenantVersion.GetProperty("id").GetInt64());

        using var primaryTenantVersionFromSecondTenant = await SendWithBearerAsync(
            client,
            $"/api/translation-versions/{versions.PrimaryVersionId}",
            secondTenantToken);
        Assert.Equal(HttpStatusCode.NotFound, primaryTenantVersionFromSecondTenant.StatusCode);
        await AssertErrorResponseAsync(primaryTenantVersionFromSecondTenant);
    }

    /// <summary>
    /// Verifies validation and not-found cases return the platform error envelope.
    /// </summary>
    [Fact]
    public async Task AssetsReadEndpoints_WithInvalidFiltersOrMissingResources_ReturnErrors()
    {
        var deleted = await SeedStoryAsync("special_story", "deleted_asset", "已删除资源剧情集", 1, deleted: true);
        using var client = apiFactory.CreateClient();
        var login = await LoginAsync(
            client,
            IntegrationTestDatabaseFixture.AdminQqId,
            IntegrationTestDatabaseFixture.AdminPassword);

        using var invalidType = await SendWithBearerAsync(
            client,
            "/api/story-groups?story_type=bad_story",
            login.Token);
        Assert.Equal(HttpStatusCode.BadRequest, invalidType.StatusCode);
        await AssertErrorResponseAsync(invalidType);

        using var invalidPage = await SendWithBearerAsync(
            client,
            "/api/stories?page=abc&page_size=101",
            login.Token);
        Assert.Equal(HttpStatusCode.BadRequest, invalidPage.StatusCode);
        await AssertErrorResponseAsync(invalidPage);

        using var invalidStoryGroup = await SendWithBearerAsync(
            client,
            "/api/stories?story_group_id=abc",
            login.Token);
        Assert.Equal(HttpStatusCode.BadRequest, invalidStoryGroup.StatusCode);
        await AssertErrorResponseAsync(invalidStoryGroup);

        using var overflowPage = await SendWithBearerAsync(
            client,
            "/api/story-groups?page=101&page_size=100",
            login.Token);
        Assert.Equal(HttpStatusCode.BadRequest, overflowPage.StatusCode);
        await AssertErrorResponseAsync(overflowPage);

        using var missingGroup = await SendWithBearerAsync(client, "/api/story-groups/999999999", login.Token);
        Assert.Equal(HttpStatusCode.NotFound, missingGroup.StatusCode);
        await AssertErrorResponseAsync(missingGroup);

        using var deletedStory = await SendWithBearerAsync(client, $"/api/stories/{deleted.StoryId}", login.Token);
        Assert.Equal(HttpStatusCode.NotFound, deletedStory.StatusCode);
        await AssertErrorResponseAsync(deletedStory);

        using var missingSourceLines = await SendWithBearerAsync(client, "/api/stories/999999999/source-lines", login.Token);
        Assert.Equal(HttpStatusCode.NotFound, missingSourceLines.StatusCode);
        await AssertErrorResponseAsync(missingSourceLines);

        using var deletedStoryVersions = await SendWithBearerAsync(
            client,
            $"/api/stories/{deleted.StoryId}/translation-versions",
            login.Token);
        Assert.Equal(HttpStatusCode.NotFound, deletedStoryVersions.StatusCode);
        await AssertErrorResponseAsync(deletedStoryVersions);
    }

    /// <summary>
    /// Verifies API Service delegates asset reads with the expected internal token scope.
    /// </summary>
    [Fact]
    public async Task AssetsProxy_UsesAssetsReadInternalToken()
    {
        var assetHandler = new FakeAssetServiceHandler();
        using var api = new ApiServiceFactory(fixture.ConnectionString, authFactory, assetHandler);
        using var client = api.CreateClient();
        var login = await LoginAsync(
            client,
            IntegrationTestDatabaseFixture.AdminQqId,
            IntegrationTestDatabaseFixture.AdminPassword);

        using var response = await SendWithBearerAsync(client, "/api/story-types", login.Token);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var token = Assert.Single(assetHandler.InternalTokens);
        AssertAssetsReadToken(token, login.UserId, login.TenantId);
    }

    /// <summary>
    /// Ensures public asset endpoints require authentication and a selected tenant before proxying.
    /// </summary>
    [Theory]
    [MemberData(nameof(PublicAssetPaths))]
    public async Task AssetsProxy_RequiresSelectedTenantBeforeForwarding(string path)
    {
        var assetHandler = new FakeAssetServiceHandler();
        using var api = new ApiServiceFactory(fixture.ConnectionString, authFactory, assetHandler);
        using var client = api.CreateClient();

        using var anonymous = await client.GetAsync(path);
        Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);

        var login = await LoginWithoutTenantAsync(
            client,
            IntegrationTestDatabaseFixture.MultiTenantUserQqId,
            IntegrationTestDatabaseFixture.MultiTenantUserPassword);
        var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", login.Token);
        request.Headers.Add("X-Sekai-User-Id", "1");
        request.Headers.Add("X-Sekai-Tenant-Id", "1");
        using var noTenant = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Forbidden, noTenant.StatusCode);
        Assert.Empty(assetHandler.InternalTokens);
    }

    /// <summary>
    /// Ensures Asset Service rejects internal asset reads without the required actor, scope, and context.
    /// </summary>
    [Theory]
    [MemberData(nameof(InvalidInternalAssetTokens))]
    public async Task AssetsInternalEndpoints_RejectInvalidInternalTokens(string? token, HttpStatusCode expectedStatus)
    {
        using var client = assetFactory.CreateClient();
        var request = new HttpRequestMessage(HttpMethod.Get, "/internal/story-types");
        if (token is not null)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        using var response = await client.SendAsync(request);

        Assert.Equal(expectedStatus, response.StatusCode);
    }

    /// <summary>
    /// Ensures Asset Service rejects a valid internal token when the delegated user is not a tenant member.
    /// </summary>
    [Fact]
    public async Task AssetsInternalEndpoints_RejectInactiveTenantMembership()
    {
        await using var dbContext = fixture.CreateDbContext();
        var adminId = await dbContext.Users
            .Where(user => user.QqId == IntegrationTestDatabaseFixture.AdminQqId)
            .Select(user => user.Id)
            .SingleAsync();
        var secondTenantId = await dbContext.Tenants
            .Where(tenant => tenant.Name == IntegrationTestDatabaseFixture.SecondTenantName)
            .Select(tenant => tenant.Id)
            .SingleAsync();
        using var client = assetFactory.CreateClient();
        var request = new HttpRequestMessage(HttpMethod.Get, "/internal/story-types");
        request.Headers.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            IntegrationTestInternalAuth.Issue(
                SekaiInternalAuthDefaults.ApiServiceActor,
                SekaiInternalAuthDefaults.AssetServiceActor,
                SekaiInternalAuthDefaults.AssetsReadScope,
                adminId,
                secondTenantId));

        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }


    /// <inheritdoc />
    public void Dispose()
    {
        apiFactory.Dispose();
        assetFactory.Dispose();
        authFactory.Dispose();
    }

    /// <summary>
    /// Seeds one story group, story, and source lines for asset read tests.
    /// </summary>
    private async Task<SeededStory> SeedStoryAsync(
        string storyType,
        string scenarioPrefix,
        string groupTitle,
        int lineCount,
        bool deleted = false)
    {
        await using var dbContext = fixture.CreateDbContext();
        var now = DateTimeOffset.UtcNow;
        var suffix = Guid.NewGuid().ToString("N");
        var group = new StoryGroup
        {
            StoryType = storyType,
            ExternalType = "special",
            ExternalId = $"{scenarioPrefix}_group_{suffix}",
            DisplayNo = 8,
            Title = groupTitle,
            Subtitle = "Asset read subtitle",
            Metadata = """{"kind":"asset-read"}""",
            CreatedAt = now,
            UpdatedAt = now,
            DeletedAt = deleted ? now : null
        };
        var story = new Story
        {
            Group = group,
            StoryType = storyType,
            ScenarioId = $"{scenarioPrefix}_{suffix}",
            Title = "资源剧情",
            SortOrder = 1,
            Metadata = """{"kind":"story"}""",
            CreatedAt = now,
            UpdatedAt = now,
            DeletedAt = deleted ? now : null
        };
        dbContext.StoryGroups.Add(group);
        dbContext.Stories.Add(story);

        for (var lineNo = 1; lineNo <= lineCount; lineNo++)
        {
            dbContext.StorySourceLines.Add(new StorySourceLine
            {
                Story = story,
                LineNo = lineNo,
                LineType = "dialogue",
                Speaker = $"Speaker {lineNo}",
                Text = $"Source line {lineNo}",
                Metadata = """{"kind":"source"}""",
                CreatedAt = now,
                UpdatedAt = now
            });
        }

        await dbContext.SaveChangesAsync();
        var sourceLineIds = await dbContext.StorySourceLines
            .Where(line => line.StoryId == story.Id)
            .OrderBy(line => line.LineNo)
            .Select(line => line.Id)
            .ToArrayAsync();
        return new SeededStory(group.Id, story.Id, story.ScenarioId, group.ExternalId, sourceLineIds);
    }

    /// <summary>
    /// Seeds one current-tenant version and one other-tenant version for isolation checks.
    /// </summary>
    private async Task<SeededVersions> SeedTenantTranslationsAsync(long storyId, IReadOnlyList<long> sourceLineIds)
    {
        await using var dbContext = fixture.CreateDbContext();
        var now = DateTimeOffset.UtcNow;
        var tenantId = await dbContext.Tenants
            .Where(tenant => tenant.Name == IntegrationTestDatabaseFixture.TenantName)
            .Select(tenant => tenant.Id)
            .SingleAsync();
        var otherTenantId = await dbContext.Tenants
            .Where(tenant => tenant.Name == IntegrationTestDatabaseFixture.SecondTenantName)
            .Select(tenant => tenant.Id)
            .SingleAsync();
        var adminId = await dbContext.Users
            .Where(user => user.QqId == IntegrationTestDatabaseFixture.AdminQqId)
            .Select(user => user.Id)
            .SingleAsync();
        var multiTenantUserId = await dbContext.Users
            .Where(user => user.QqId == IntegrationTestDatabaseFixture.MultiTenantUserQqId)
            .Select(user => user.Id)
            .SingleAsync();

        var primary = new TranslationVersion
        {
            TenantId = tenantId,
            StoryId = storyId,
            VersionNo = 1,
            Title = "当前租户译文",
            Metadata = """{"staff":{"translator":"翻译A","proofreader":"校对B","approver":"合意C"}}""",
            CreatedBy = adminId,
            CreatedAt = now,
            UpdatedAt = now
        };
        var other = new TranslationVersion
        {
            TenantId = otherTenantId,
            StoryId = storyId,
            VersionNo = 1,
            Title = "其他租户译文",
            CreatedBy = multiTenantUserId,
            CreatedAt = now,
            UpdatedAt = now
        };
        dbContext.TranslationVersions.AddRange(primary, other);

        for (var i = 0; i < sourceLineIds.Count; i++)
        {
            dbContext.TranslationLines.Add(new TranslationLine
            {
                Version = primary,
                SourceLineId = sourceLineIds[i],
                StoryId = storyId,
                LineNo = i + 1,
                Speaker = $"译者 {i + 1}",
                Text = $"译文 {i + 1}",
                Metadata = """{"kind":"translation"}""",
                CreatedAt = now,
                UpdatedAt = now
            });
        }

        dbContext.TranslationLines.Add(new TranslationLine
        {
            Version = other,
            SourceLineId = sourceLineIds[0],
            StoryId = storyId,
            LineNo = 1,
            Text = "其他租户译文",
            CreatedAt = now,
            UpdatedAt = now
        });

        await dbContext.SaveChangesAsync();
        return new SeededVersions(primary.Id, other.Id, otherTenantId);
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
    /// Logs in a multi-tenant user before tenant selection.
    /// </summary>
    private static async Task<LoginWithoutTenantResult> LoginWithoutTenantAsync(
        HttpClient client,
        string username,
        string password)
    {
        using var response = await client.PostAsJsonAsync("/api/auth/login", new
        {
            username,
            password
        });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var json = await ReadJsonAsync(response);
        Assert.Equal(JsonValueKind.Null, json.RootElement.GetProperty("current_tenant").ValueKind);
        return new LoginWithoutTenantResult(
            json.RootElement.GetProperty("access_token").GetString()!,
            json.RootElement.GetProperty("user").GetProperty("id").GetInt64());
    }

    /// <summary>
    /// Logs in and switches a multi-tenant user to the requested tenant.
    /// </summary>
    private static async Task<string> LoginAndSwitchTenantAsync(
        HttpClient client,
        string username,
        string password,
        long tenantId)
    {
        var login = await LoginWithoutTenantAsync(client, username, password);
        var request = new HttpRequestMessage(HttpMethod.Put, "/api/auth/current-tenant")
        {
            Content = JsonContent.Create(new { tenant_id = tenantId })
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", login.Token);
        using var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var json = await ReadJsonAsync(response);
        return json.RootElement.GetProperty("access_token").GetString()!;
    }

    /// <summary>
    /// Sends an authenticated GET request to API Service.
    /// </summary>
    private static Task<HttpResponseMessage> SendWithBearerAsync(HttpClient client, string path, string token)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, path);
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
    /// Gets public asset paths used to verify the API Service authorization boundary.
    /// </summary>
    public static TheoryData<string> PublicAssetPaths()
    {
        return new TheoryData<string>
        {
            "/api/story-types",
            "/api/story-groups",
            "/api/story-groups/1",
            "/api/stories",
            "/api/stories/1",
            "/api/stories/1/source-lines",
            "/api/stories/1/translation-versions",
            "/api/translation-versions/1",
            "/api/translation-versions/1/lines"
        };
    }

    /// <summary>
    /// Gets invalid internal tokens used to verify Asset Service authorization.
    /// </summary>
    public static TheoryData<string?, HttpStatusCode> InvalidInternalAssetTokens()
    {
        return new TheoryData<string?, HttpStatusCode>
        {
            { null, HttpStatusCode.Unauthorized },
            {
                IntegrationTestInternalAuth.Issue(
                    SekaiInternalAuthDefaults.AssetServiceActor,
                    SekaiInternalAuthDefaults.AssetServiceActor,
                    SekaiInternalAuthDefaults.AssetsReadScope,
                    subjectUserId: 1,
                    tenantId: 1),
                HttpStatusCode.Forbidden
            },
            {
                IntegrationTestInternalAuth.Issue(
                    SekaiInternalAuthDefaults.ApiServiceActor,
                    SekaiInternalAuthDefaults.AssetServiceActor,
                    SekaiInternalAuthDefaults.SyncJobsReadScope,
                    subjectUserId: 1,
                    tenantId: 1),
                HttpStatusCode.Forbidden
            },
            {
                IntegrationTestInternalAuth.Issue(
                    SekaiInternalAuthDefaults.ApiServiceActor,
                    SekaiInternalAuthDefaults.AssetServiceActor,
                    SekaiInternalAuthDefaults.AssetsReadScope,
                    tenantId: 1),
                HttpStatusCode.Forbidden
            },
            {
                IntegrationTestInternalAuth.Issue(
                    SekaiInternalAuthDefaults.ApiServiceActor,
                    SekaiInternalAuthDefaults.AssetServiceActor,
                    SekaiInternalAuthDefaults.AssetsReadScope,
                    subjectUserId: 1),
                HttpStatusCode.Forbidden
            }
        };
    }

    private static void AssertAssetsReadToken(string? token, long expectedUserId, long expectedTenantId)
    {
        Assert.False(string.IsNullOrWhiteSpace(token));
        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(token);
        Assert.Equal(SekaiInternalAuthDefaults.ApiServiceActor, jwt.Claims.Single(claim =>
            claim.Type == SekaiInternalAuthDefaults.ActorClaimType).Value);
        Assert.Equal(SekaiInternalAuthDefaults.AssetsReadScope, jwt.Claims.Single(claim =>
            claim.Type == SekaiInternalAuthDefaults.ScopeClaimType).Value);
        Assert.Contains(SekaiInternalAuthDefaults.AssetServiceActor, jwt.Audiences);
        Assert.Equal(expectedTenantId.ToString(), jwt.Claims.Single(claim =>
            claim.Type == SekaiAuthDefaults.TenantIdClaimType).Value);
        Assert.Equal(expectedUserId.ToString(), jwt.Claims.Single(claim =>
            claim.Type == SekaiInternalAuthDefaults.SubjectUserIdClaimType).Value);
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
    /// Hosts the Asset service with test configuration and the shared database.
    /// </summary>
    private sealed class AssetServiceFactory(string connectionString) : WebApplicationFactory<AssetServiceProgram>
    {
        /// <summary>
        /// Injects integration-test configuration before the Asset service starts.
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
    /// Captures API Service calls to Asset Service for proxy-token assertions.
    /// </summary>
    private sealed class FakeAssetServiceHandler : HttpMessageHandler
    {
        /// <summary>
        /// Gets captured internal bearer tokens sent by API Service.
        /// </summary>
        public List<string?> InternalTokens { get; } = [];

        /// <summary>
        /// Records asset read requests and returns a successful empty response.
        /// </summary>
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            InternalTokens.Add(request.Headers.Authorization?.Parameter);
            if (request.RequestUri?.AbsolutePath == "/internal/story-types")
            {
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("[]", Encoding.UTF8, "application/json")
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
    /// Captures login credentials before tenant selection.
    /// </summary>
    private sealed record LoginWithoutTenantResult(string Token, long UserId);

    /// <summary>
    /// Captures seeded story identifiers used by asset read requests.
    /// </summary>
    private sealed record SeededStory(
        long GroupId,
        long StoryId,
        string ScenarioId,
        string? ExternalId,
        IReadOnlyList<long> SourceLineIds);

    /// <summary>
    /// Captures seeded translation version identifiers for tenant isolation checks.
    /// </summary>
    private sealed record SeededVersions(long PrimaryVersionId, long OtherTenantVersionId, long OtherTenantId);
}
