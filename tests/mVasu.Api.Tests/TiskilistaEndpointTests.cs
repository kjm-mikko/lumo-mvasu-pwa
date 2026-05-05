using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using mVasu.Api.Contracts;
using mVasu.Api.Tiskilista;

namespace mVasu.Api.Tests;

public class TiskilistaEndpointTests : IClassFixture<AuthenticatedWebApplicationFactory>
{
    private readonly AuthenticatedWebApplicationFactory _factory;

    public TiskilistaEndpointTests(AuthenticatedWebApplicationFactory factory) => _factory = factory;

    // -- GET /api/tiskilista ---------------------------------------------------

    [Fact]
    public async Task GetList_WithoutToken_Returns401()
    {
        var client = WithMock(new RecordingService()).CreateClient();

        var response = await client.GetAsync("/api/tiskilista");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetList_AuthenticatedWithoutScope_Returns403()
    {
        var client = WithMock(new RecordingService()).CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-User", "mikko.nieminen@kojamo.fi");
        client.DefaultRequestHeaders.Add("X-Test-NoScope", "1");

        var response = await client.GetAsync("/api/tiskilista");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Theory]
    [InlineData("scope=invalid")]
    [InlineData("sortBy=neon")]
    [InlineData("page=0")]
    [InlineData("pageSize=0")]
    [InlineData("pageSize=500")]
    public async Task GetList_InvalidQuery_Returns400(string queryString)
    {
        var mock = new RecordingService();
        var client = WithMock(mock).CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-User", "mikko.nieminen@kojamo.fi");

        var response = await client.GetAsync($"/api/tiskilista?{queryString}");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Null(mock.LastListQuery); // service should not be invoked
    }

    [Fact]
    public async Task GetList_DefaultsScopeToOmatAndSortByVapautuu()
    {
        var mock = new RecordingService
        {
            ListResult = new TiskilistaPageDto(Array.Empty<TiskilistaCardDto>(), 0, 1, 20),
        };
        var client = WithMock(mock).CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-User", "mikko.nieminen@kojamo.fi");

        var response = await client.GetAsync("/api/tiskilista");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(mock.LastListQuery);
        Assert.Equal("omat", mock.LastListQuery.Scope);
        Assert.Equal("vapautuu", mock.LastListQuery.SortBy);
        Assert.Equal(1, mock.LastListQuery.Page);
        Assert.Equal(20, mock.LastListQuery.PageSize);
    }

    [Fact]
    public async Task GetList_PassesQueryParametersIntoService()
    {
        var mock = new RecordingService
        {
            ListResult = new TiskilistaPageDto(Array.Empty<TiskilistaCardDto>(), 0, 2, 10),
        };
        var client = WithMock(mock).CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-User", "mikko.nieminen@kojamo.fi");

        var response = await client.GetAsync(
            "/api/tiskilista?q=mannerheim&status=Vapaa&scope=kaikki&sortBy=osoite&page=2&pageSize=10");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(mock.LastListQuery);
        Assert.Equal("mannerheim", mock.LastListQuery.Search);
        Assert.Equal("Vapaa", mock.LastListQuery.Status);
        Assert.Equal("kaikki", mock.LastListQuery.Scope);
        Assert.Equal("osoite", mock.LastListQuery.SortBy);
        Assert.Equal(2, mock.LastListQuery.Page);
        Assert.Equal(10, mock.LastListQuery.PageSize);
    }

    [Fact]
    public async Task GetList_DistanceSortPicksUpUserCoordinates()
    {
        var mock = new RecordingService
        {
            ListResult = new TiskilistaPageDto(Array.Empty<TiskilistaCardDto>(), 0, 1, 20),
        };
        var client = WithMock(mock).CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-User", "mikko.nieminen@kojamo.fi");

        var response = await client.GetAsync("/api/tiskilista?sortBy=distance&userLat=60.17&userLon=24.94");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("distance", mock.LastListQuery!.SortBy);
        Assert.Equal(60.17, mock.LastListQuery.UserLat);
        Assert.Equal(24.94, mock.LastListQuery.UserLon);
    }

    [Fact]
    public async Task GetList_UserNotProvisioned_Returns403()
    {
        var mock = new RecordingService { ListResult = null };
        var client = WithMock(mock).CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-User", "ghost@kojamo.fi");

        var response = await client.GetAsync("/api/tiskilista");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    // -- GET /api/tiskilista/{id} ----------------------------------------------

    [Fact]
    public async Task GetById_AuthenticatedAndFound_Returns200WithDetail()
    {
        var id = Guid.NewGuid();
        var mock = new RecordingService
        {
            DetailResult = new TiskilistaDetailDto(
                Id: id,
                Osoite: "Mannerheimintie 1",
                Kptunnus: 10086,
                Huonetunnus: 1024,
                Postinumero: "00100",
                Postitoimipaikka: "Helsinki",
                Tyyppi: "2H+K",
                Laji: "Asuinhuoneisto",
                Vuokra: 950,
                Vapautuu: DateTimeOffset.Now.AddDays(30),
                Poismuutto: null,
                VapautuuAsiakkaalta: null,
                RemonttiAlkaa: null,
                RemonttiPaattyy: null,
                Remonttityyppi: null,
                Neliot: 47.5f,
                Kerros: "3",
                Kerroksia: "5",
                Tila: "Vapaa",
                SopimusTila: null,
                Kunta: "Helsinki",
                Kaupunginosa: "Kamppi",
                Markkinointialue: null,
                Prio: null,
                Isannoitsija: null,
                Markkinoija: null,
                TarkastusTila: null,
                LumoFi: true,
                Vuokraovi: true,
                OnKuvausTarve: false,
                Muistio: null,
                HuoneistoMuistio: null,
                Kuvaus: null,
                LisaTieto: null,
                BrochureUrl: null,
                Hissi: true,
                Parveke: false,
                Sauna: false,
                YhteissaUna: true,
                Vesimittaus: false,
                Pesula: false,
                Astianpesukone: true,
                Aluetoimisto: "Helsinki",
                NextEsittelyAt: null,
                Latitude: 60.17,
                Longitude: 24.93,
                DistanceKm: null,
                LumoUrl: null),
        };
        var client = WithMock(mock).CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-User", "mikko.nieminen@kojamo.fi");

        var response = await client.GetAsync($"/api/tiskilista/{id}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var dto = await response.Content.ReadFromJsonAsync<TiskilistaDetailDto>();
        Assert.NotNull(dto);
        Assert.Equal(id, dto.Id);
        Assert.Equal("Mannerheimintie 1", dto.Osoite);
        Assert.Equal(id, mock.LastDetailId);
    }

    [Fact]
    public async Task GetById_NotFound_Returns404()
    {
        var mock = new RecordingService { DetailResult = null };
        var client = WithMock(mock).CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-User", "mikko.nieminen@kojamo.fi");

        var response = await client.GetAsync($"/api/tiskilista/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // -- helpers ---------------------------------------------------------------

    private WebApplicationFactory<Program> WithMock(ITiskilistaQueryService mock) =>
        _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureTestServices(services =>
            {
                services.Replace(ServiceDescriptor.Scoped(_ => mock));
            });
        });

    private sealed class RecordingService : ITiskilistaQueryService
    {
        public TiskilistaPageDto? ListResult { get; set; } = new(Array.Empty<TiskilistaCardDto>(), 0, 1, 20);
        public TiskilistaDetailDto? DetailResult { get; set; }
        public TiskilistaListQuery? LastListQuery { get; private set; }
        public Guid? LastDetailId { get; private set; }

        public Task<TiskilistaPageDto?> ListAsync(
            ClaimsPrincipal principal,
            TiskilistaListQuery query,
            CancellationToken cancellationToken = default)
        {
            LastListQuery = query;
            return Task.FromResult(ListResult);
        }

        public Task<TiskilistaDetailDto?> GetAsync(
            ClaimsPrincipal principal,
            Guid id,
            double? userLat = null,
            double? userLon = null,
            CancellationToken cancellationToken = default)
        {
            LastDetailId = id;
            return Task.FromResult(DetailResult);
        }

        public TiskilistaDistinctValuesDto? DistinctValuesResult { get; set; } =
            new TiskilistaDistinctValuesDto(
                Array.Empty<string>(), Array.Empty<string>(), Array.Empty<string>(),
                Array.Empty<string>(), Array.Empty<string>(), Array.Empty<string>(),
                Array.Empty<string>(), Array.Empty<string>());

        public Task<TiskilistaDistinctValuesDto?> GetDistinctValuesAsync(
            ClaimsPrincipal principal,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(DistinctValuesResult);
        }
    }
}
