using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using mVasu.Api.Contracts;
using mVasu.Api.Search;

namespace mVasu.Api.Tests;

public class SearchEndpointTests : IClassFixture<AuthenticatedWebApplicationFactory>
{
    private readonly AuthenticatedWebApplicationFactory _factory;

    public SearchEndpointTests(AuthenticatedWebApplicationFactory factory) =>
        _factory = factory;

    [Fact]
    public async Task Search_WithoutToken_Returns401()
    {
        var client = WithMock(new RecordingService()).CreateClient();

        var response = await client.GetAsync("/api/search?q=manner");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Search_AuthenticatedWithoutScope_Returns403()
    {
        var client = WithMock(new RecordingService()).CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-User", "mikko.nieminen@kojamo.fi");
        client.DefaultRequestHeaders.Add("X-Test-NoScope", "1");

        var response = await client.GetAsync("/api/search?q=manner");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Theory]
    [InlineData("limit=0")]
    [InlineData("limit=99")]
    public async Task Search_InvalidLimit_Returns400(string queryString)
    {
        var mock = new RecordingService();
        var client = WithMock(mock).CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-User", "mikko.nieminen@kojamo.fi");

        var response = await client.GetAsync($"/api/search?q=manner&{queryString}");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Null(mock.LastQuery);
    }

    [Fact]
    public async Task Search_WithoutQuery_PassesEmptyToService()
    {
        var mock = new RecordingService { Result = Empty() };
        var client = WithMock(mock).CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-User", "mikko.nieminen@kojamo.fi");

        var response = await client.GetAsync("/api/search");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(mock.LastQuery);
        Assert.Null(mock.LastQuery.Q);
        Assert.Equal(5, mock.LastQuery.Limit); // default
    }

    [Fact]
    public async Task Search_PassesQueryAndLimit()
    {
        var mock = new RecordingService { Result = Empty() };
        var client = WithMock(mock).CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-User", "mikko.nieminen@kojamo.fi");

        var response = await client.GetAsync("/api/search?q=manner&limit=10");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("manner", mock.LastQuery?.Q);
        Assert.Equal(10, mock.LastQuery?.Limit);
    }

    [Fact]
    public async Task Search_ReturnsPayloadShape()
    {
        var mock = new RecordingService
        {
            Result = new SearchResponseDto(new[]
            {
                new SearchGroupDto(
                    SearchGroupIds.Units,
                    "Kohteet",
                    new[]
                    {
                        new SearchHitDto(
                            Id: "unit-1",
                            Title: "Mannerheimintie 12 A 4",
                            Meta: "2 h · 47 m² · vapaa 1.7.",
                            Icon: "home",
                            Navigate: "/tiskilista"),
                    }),
                new SearchGroupDto(
                    SearchGroupIds.Actions,
                    "Toiminnot",
                    new[]
                    {
                        new SearchHitDto(
                            Id: "action-new-task",
                            Title: "Lisää uusi tehtävä",
                            Meta: "Luo Tehtava xVasuSecuritySystemUser:lle",
                            Icon: "check",
                            Navigate: "/tasks"),
                    }),
            }),
        };
        var client = WithMock(mock).CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-User", "mikko.nieminen@kojamo.fi");

        var response = await client.GetAsync("/api/search?q=manner");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<SearchResponseDto>();
        Assert.NotNull(body);
        Assert.Equal(2, body!.Groups.Count);
        Assert.Equal(SearchGroupIds.Units, body.Groups[0].Id);
        Assert.Equal("Mannerheimintie 12 A 4", body.Groups[0].Hits[0].Title);
        Assert.Equal(SearchGroupIds.Actions, body.Groups[1].Id);
        Assert.Equal("/tasks", body.Groups[1].Hits[0].Navigate);
    }

    private static SearchResponseDto Empty() =>
        new(Array.Empty<SearchGroupDto>());

    private WebApplicationFactory<Program> WithMock(ISearchService mock) =>
        _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureTestServices(services =>
            {
                services.Replace(ServiceDescriptor.Scoped(_ => mock));
            });
        });

    private sealed class RecordingService : ISearchService
    {
        public SearchResponseDto Result { get; set; } =
            new(Array.Empty<SearchGroupDto>());

        public SearchQueryParameters? LastQuery { get; private set; }

        public Task<SearchResponseDto> SearchAsync(
            ClaimsPrincipal principal,
            SearchQueryParameters query,
            CancellationToken cancellationToken = default)
        {
            LastQuery = query;
            return Task.FromResult(Result);
        }
    }
}
