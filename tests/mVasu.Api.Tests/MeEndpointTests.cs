using System.Net;
using System.Net.Http.Json;
using mVasu.Api.Contracts;

namespace mVasu.Api.Tests;

public class MeEndpointTests : IClassFixture<AuthenticatedWebApplicationFactory>
{
    private readonly AuthenticatedWebApplicationFactory _factory;

    public MeEndpointTests(AuthenticatedWebApplicationFactory factory) => _factory = factory;

    [Fact]
    public async Task GetMe_WithoutToken_Returns401()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/me");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetMe_AuthenticatedWithoutScope_Returns403()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-User", "mikko.nieminen@kojamo.fi");
        client.DefaultRequestHeaders.Add("X-Test-NoScope", "1");

        var response = await client.GetAsync("/api/me");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task GetMe_AuthenticatedWithScope_ReturnsProfile()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-User", "mikko.nieminen@kojamo.fi");

        var response = await client.GetAsync("/api/me");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var dto = await response.Content.ReadFromJsonAsync<UserProfileDto>();
        Assert.NotNull(dto);
        Assert.Equal("mikko.nieminen@kojamo.fi", dto.Email);
        Assert.Equal("Mikko Nieminen", dto.DisplayName);
        Assert.Null(dto.PreferredName);
        Assert.Equal("light", dto.Theme);
        Assert.Equal("fi", dto.Language);
        Assert.False(dto.LocationConsent);
    }
}
