using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using mVasu.Api.Contracts;

namespace mVasu.Api.Tests;

public class HealthEndpointTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public HealthEndpointTests(WebApplicationFactory<Program> factory) => _factory = factory;

    [Fact]
    public async Task GetHealth_Returns200_WithHealthyStatusAndVersion()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var dto = await response.Content.ReadFromJsonAsync<HealthCheckDto>();
        Assert.NotNull(dto);
        Assert.Equal("Healthy", dto.Status);
        Assert.False(string.IsNullOrWhiteSpace(dto.Version));
        Assert.True(dto.UptimeSeconds >= 0);
        Assert.True(dto.Timestamp <= DateTimeOffset.UtcNow);
    }
}
