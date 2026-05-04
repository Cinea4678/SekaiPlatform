using Microsoft.AspNetCore.Mvc.Testing;

namespace SekaiPlatform.IntegrationTests;

/// <summary>
/// Verifies the API service health endpoint through the in-memory test host.
/// </summary>
public sealed class ApiHealthTests(WebApplicationFactory<Program> factory)
    : IClassFixture<WebApplicationFactory<Program>>
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
}
