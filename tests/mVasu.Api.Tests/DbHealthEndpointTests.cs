using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using mVasu.Api.Contracts;
using mVasu.Api.Data;

namespace mVasu.Api.Tests;

public class DbHealthEndpointTests : IClassFixture<AuthenticatedWebApplicationFactory>
{
    private readonly AuthenticatedWebApplicationFactory _factory;

    public DbHealthEndpointTests(AuthenticatedWebApplicationFactory factory) => _factory = factory;

    [Fact]
    public async Task GetDbHealth_WhenConnected_ReturnsOkAndAnonymous()
    {
        var factory = _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IDbHealthCheck>();
                services.AddSingleton<IDbHealthCheck, AlwaysHealthyDbCheck>();
            });
        });

        var client = factory.CreateClient();

        var response = await client.GetAsync("/api/health/db");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var dto = await response.Content.ReadFromJsonAsync<DbHealthCheckDto>();
        Assert.NotNull(dto);
        Assert.True(dto.Connected);
        Assert.Null(dto.Error);
    }

    [Fact]
    public async Task GetDbHealth_WhenDisconnected_Returns503()
    {
        var factory = _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IDbHealthCheck>();
                services.AddSingleton<IDbHealthCheck, AlwaysFailingDbCheck>();
            });
        });

        var client = factory.CreateClient();

        var response = await client.GetAsync("/api/health/db");

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        var dto = await response.Content.ReadFromJsonAsync<DbHealthCheckDto>();
        Assert.NotNull(dto);
        Assert.False(dto.Connected);
        Assert.Equal("SqlException", dto.Error);
    }

    private sealed class AlwaysHealthyDbCheck : IDbHealthCheck
    {
        public Task<DbHealthCheckDto> CheckAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(new DbHealthCheckDto(Connected: true, DurationMs: 1.0, Error: null));
    }

    private sealed class AlwaysFailingDbCheck : IDbHealthCheck
    {
        public Task<DbHealthCheckDto> CheckAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(new DbHealthCheckDto(Connected: false, DurationMs: 5.0, Error: "SqlException"));
    }
}
