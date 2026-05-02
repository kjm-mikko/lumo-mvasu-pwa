using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using mVasu.Api.Authentication;
using mVasu.Api.Contracts;

namespace mVasu.Api.Tests;

public class SettingsEndpointTests : IClassFixture<AuthenticatedWebApplicationFactory>
{
    private readonly AuthenticatedWebApplicationFactory _factory;

    public SettingsEndpointTests(AuthenticatedWebApplicationFactory factory) => _factory = factory;

    [Fact]
    public async Task PutSettings_WithoutToken_Returns401()
    {
        var client = WithMockSettingsService(new RecordingSettingsService()).CreateClient();

        var response = await client.PutAsJsonAsync(
            "/api/me/settings",
            new UpdateSettingsDto(null, "light", "fi"));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task PutSettings_AuthenticatedWithoutScope_Returns403()
    {
        var client = WithMockSettingsService(new RecordingSettingsService()).CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-User", "mikko.nieminen@kojamo.fi");
        client.DefaultRequestHeaders.Add("X-Test-NoScope", "1");

        var response = await client.PutAsJsonAsync(
            "/api/me/settings",
            new UpdateSettingsDto(null, "light", "fi"));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Theory]
    [InlineData("neon", "fi", null)]
    [InlineData("light", "sv", null)]
    [InlineData("light", "fi", "WAY_TOO_LONG_PREFERRED_NAME_____________________________________________________________________________________xx")]
    [InlineData("", "fi", null)]
    [InlineData("light", "", null)]
    public async Task PutSettings_InvalidPayload_Returns400(string theme, string language, string? preferredName)
    {
        var mock = new RecordingSettingsService();
        var client = WithMockSettingsService(mock).CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-User", "mikko.nieminen@kojamo.fi");

        var response = await client.PutAsJsonAsync(
            "/api/me/settings",
            new UpdateSettingsDto(preferredName, theme, language));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Null(mock.LastDto); // service should not have been invoked
    }

    [Fact]
    public async Task PutSettings_ValidPayload_Returns200_AndUpdatedProfile()
    {
        var mock = new RecordingSettingsService
        {
            Result = new UserProfileDto(
                Id: Guid.NewGuid(),
                Email: "mikko.nieminen@kojamo.fi",
                DisplayName: "Mikko Nieminen",
                PreferredName: "Mikko",
                Theme: "dark",
                Language: "en",
                LocationConsent: false),
        };

        var client = WithMockSettingsService(mock).CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-User", "mikko.nieminen@kojamo.fi");

        var response = await client.PutAsJsonAsync(
            "/api/me/settings",
            new UpdateSettingsDto("Mikko", "dark", "en"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var dto = await response.Content.ReadFromJsonAsync<UserProfileDto>();
        Assert.NotNull(dto);
        Assert.Equal("dark", dto.Theme);
        Assert.Equal("en", dto.Language);
        Assert.Equal("Mikko", dto.PreferredName);

        Assert.NotNull(mock.LastDto);
        Assert.Equal("Mikko", mock.LastDto.PreferredName);
        Assert.Equal("dark", mock.LastDto.Theme);
        Assert.Equal("en", mock.LastDto.Language);
    }

    [Fact]
    public async Task PutSettings_UserNotProvisioned_Returns403()
    {
        var mock = new RecordingSettingsService { Result = null };
        var client = WithMockSettingsService(mock).CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-User", "ghost@kojamo.fi");

        var response = await client.PutAsJsonAsync(
            "/api/me/settings",
            new UpdateSettingsDto(null, "light", "fi"));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.NotNull(mock.LastDto); // service was called, returned null
    }

    private WebApplicationFactory<Program> WithMockSettingsService(IUserSettingsService mock)
    {
        return _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureTestServices(services =>
            {
                services.Replace(ServiceDescriptor.Scoped(_ => mock));
            });
        });
    }

    private sealed class RecordingSettingsService : IUserSettingsService
    {
        public UserProfileDto? Result { get; set; }
        public UpdateSettingsDto? LastDto { get; private set; }

        public Task<UserProfileDto?> UpdateAsync(
            ClaimsPrincipal principal,
            UpdateSettingsDto dto,
            CancellationToken cancellationToken = default)
        {
            LastDto = dto;
            return Task.FromResult(Result);
        }

        public Task<UserProfileDto?> UpdateLocationConsentAsync(
            ClaimsPrincipal principal,
            bool consent,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(Result);

        public Task<RecordLocationResult> RecordLocationAsync(
            ClaimsPrincipal principal,
            UserLocationDto location,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(RecordLocationResult.Recorded);
    }
}
