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

public class LocationEndpointTests : IClassFixture<AuthenticatedWebApplicationFactory>
{
    private readonly AuthenticatedWebApplicationFactory _factory;

    public LocationEndpointTests(AuthenticatedWebApplicationFactory factory) => _factory = factory;

    // -- PUT /api/me/location-consent -----------------------------------------

    [Fact]
    public async Task PutConsent_WithoutToken_Returns401()
    {
        var client = WithMock(new RecordingService()).CreateClient();

        var response = await client.PutAsJsonAsync(
            "/api/me/location-consent",
            new UpdateLocationConsentDto(true));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task PutConsent_AuthenticatedAndProvisioned_Returns200WithUpdatedProfile()
    {
        var mock = new RecordingService
        {
            ConsentResult = new UserProfileDto(
                Id: Guid.NewGuid(),
                Email: "mikko.nieminen@kojamo.fi",
                DisplayName: "Mikko Nieminen",
                PreferredName: null,
                Theme: "light",
                Language: "fi",
                LocationConsent: true),
        };

        var client = WithMock(mock).CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-User", "mikko.nieminen@kojamo.fi");

        var response = await client.PutAsJsonAsync(
            "/api/me/location-consent",
            new UpdateLocationConsentDto(true));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var dto = await response.Content.ReadFromJsonAsync<UserProfileDto>();
        Assert.NotNull(dto);
        Assert.True(dto.LocationConsent);
        Assert.True(mock.LastConsent);
    }

    [Fact]
    public async Task PutConsent_UserNotProvisioned_Returns403()
    {
        var mock = new RecordingService { ConsentResult = null };
        var client = WithMock(mock).CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-User", "ghost@kojamo.fi");

        var response = await client.PutAsJsonAsync(
            "/api/me/location-consent",
            new UpdateLocationConsentDto(true));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    // -- POST /api/me/location ------------------------------------------------

    [Fact]
    public async Task PostLocation_WithoutToken_Returns401()
    {
        var client = WithMock(new RecordingService()).CreateClient();

        var response = await client.PostAsJsonAsync(
            "/api/me/location",
            ValidLocation());

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Theory]
    [InlineData(91.0, 25.0)]   // latitude > 90
    [InlineData(-90.5, 25.0)]  // latitude < -90
    [InlineData(60.0, 181.0)]  // longitude > 180
    [InlineData(60.0, -181.0)] // longitude < -180
    public async Task PostLocation_OutOfRangeCoordinates_Returns400(double latitude, double longitude)
    {
        var mock = new RecordingService();
        var client = WithMock(mock).CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-User", "mikko.nieminen@kojamo.fi");

        var response = await client.PostAsJsonAsync(
            "/api/me/location",
            new UserLocationDto(latitude, longitude, Accuracy: 10, RecordedAt: DateTimeOffset.UtcNow));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.False(mock.RecordCalled);
    }

    [Fact]
    public async Task PostLocation_NegativeAccuracy_Returns400()
    {
        var mock = new RecordingService();
        var client = WithMock(mock).CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-User", "mikko.nieminen@kojamo.fi");

        var response = await client.PostAsJsonAsync(
            "/api/me/location",
            new UserLocationDto(60.17, 24.94, Accuracy: -1.0, RecordedAt: DateTimeOffset.UtcNow));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.False(mock.RecordCalled);
    }

    [Fact]
    public async Task PostLocation_FutureRecordedAt_Returns400()
    {
        var mock = new RecordingService();
        var client = WithMock(mock).CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-User", "mikko.nieminen@kojamo.fi");

        var response = await client.PostAsJsonAsync(
            "/api/me/location",
            new UserLocationDto(60.17, 24.94, Accuracy: 10, RecordedAt: DateTimeOffset.UtcNow.AddHours(1)));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.False(mock.RecordCalled);
    }

    [Fact]
    public async Task PostLocation_ValidPayload_Returns204_AndCallsService()
    {
        var mock = new RecordingService { RecordResult = RecordLocationResult.Recorded };
        var client = WithMock(mock).CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-User", "mikko.nieminen@kojamo.fi");

        var payload = ValidLocation();
        var response = await client.PostAsJsonAsync("/api/me/location", payload);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.True(mock.RecordCalled);
        Assert.Equal(payload, mock.LastLocation);
    }

    [Fact]
    public async Task PostLocation_ConsentNotGranted_Returns403()
    {
        var mock = new RecordingService { RecordResult = RecordLocationResult.ConsentNotGranted };
        var client = WithMock(mock).CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-User", "mikko.nieminen@kojamo.fi");

        var response = await client.PostAsJsonAsync("/api/me/location", ValidLocation());

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task PostLocation_UserNotProvisioned_Returns403()
    {
        var mock = new RecordingService { RecordResult = RecordLocationResult.UserNotProvisioned };
        var client = WithMock(mock).CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-User", "ghost@kojamo.fi");

        var response = await client.PostAsJsonAsync("/api/me/location", ValidLocation());

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    private static UserLocationDto ValidLocation() => new(
        Latitude: 60.1699,
        Longitude: 24.9384,
        Accuracy: 12.5,
        RecordedAt: DateTimeOffset.UtcNow.AddSeconds(-30));

    private WebApplicationFactory<Program> WithMock(IUserSettingsService mock) =>
        _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureTestServices(services =>
            {
                services.Replace(ServiceDescriptor.Scoped(_ => mock));
            });
        });

    private sealed class RecordingService : IUserSettingsService
    {
        public UserProfileDto? ConsentResult { get; set; }
        public RecordLocationResult RecordResult { get; set; } = RecordLocationResult.Recorded;
        public bool? LastConsent { get; private set; }
        public bool RecordCalled { get; private set; }
        public UserLocationDto? LastLocation { get; private set; }

        public Task<UserProfileDto?> UpdateAsync(
            ClaimsPrincipal principal,
            UpdateSettingsDto dto,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<UserProfileDto?>(null);

        public Task<UserProfileDto?> UpdateLocationConsentAsync(
            ClaimsPrincipal principal,
            bool consent,
            CancellationToken cancellationToken = default)
        {
            LastConsent = consent;
            return Task.FromResult(ConsentResult);
        }

        public Task<RecordLocationResult> RecordLocationAsync(
            ClaimsPrincipal principal,
            UserLocationDto location,
            CancellationToken cancellationToken = default)
        {
            RecordCalled = true;
            LastLocation = location;
            return Task.FromResult(RecordResult);
        }
    }
}
