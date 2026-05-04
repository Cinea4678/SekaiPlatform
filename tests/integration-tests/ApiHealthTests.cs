using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using SekaiPlatform.Shared.Web;

namespace SekaiPlatform.IntegrationTests;

/// <summary>
/// Verifies the API service health endpoint through the in-memory test host.
/// </summary>
public sealed class ApiHealthTests(ApiHealthTests.ApiServiceFactory factory)
    : IClassFixture<ApiHealthTests.ApiServiceFactory>
{
    /// <summary>
    /// Ensures the API service reports a healthy status payload.
    /// </summary>
    [Fact]
    public async Task Health_ReturnsHealthy()
    {
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/health");
        var body = await response.Content.ReadAsStringAsync();

        response.EnsureSuccessStatusCode();
        Assert.Equal("Healthy", body);
    }

    /// <summary>
    /// Hosts API Service with the internal signing key required at startup.
    /// </summary>
    public sealed class ApiServiceFactory : WebApplicationFactory<Program>
    {
        /// <summary>
        /// Injects integration-test internal token configuration.
        /// </summary>
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.ConfigureAppConfiguration((_, configuration) =>
            {
                var values = new Dictionary<string, string?>();
                IntegrationTestInternalAuth.AddConfiguration(
                    values,
                    SekaiInternalAuthDefaults.ApiServiceActor,
                    includePrivateKey: true);
                configuration.AddInMemoryCollection(values);
            });
        }
    }
}
