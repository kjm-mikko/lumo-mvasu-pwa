using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using mVasu.Api.Contracts;
using mVasu.Api.Customers;

namespace mVasu.Api.Tests;

public class CustomersEndpointTests : IClassFixture<AuthenticatedWebApplicationFactory>
{
    private readonly AuthenticatedWebApplicationFactory _factory;

    public CustomersEndpointTests(AuthenticatedWebApplicationFactory factory) =>
        _factory = factory;

    [Fact]
    public async Task GetCustomers_WithoutToken_Returns401()
    {
        var client = WithMock(new RecordingService()).CreateClient();

        var response = await client.GetAsync("/api/customers");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetCustomers_AuthenticatedWithoutScope_Returns403()
    {
        var client = WithMock(new RecordingService()).CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-User", "mikko.nieminen@kojamo.fi");
        client.DefaultRequestHeaders.Add("X-Test-NoScope", "1");

        var response = await client.GetAsync("/api/customers");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Theory]
    [InlineData("type=tenant")]
    [InlineData("type=invalid")]
    [InlineData("relation=has-everything")]
    [InlineData("relation=foo")]
    public async Task GetCustomers_InvalidQuery_Returns400(string queryString)
    {
        var mock = new RecordingService();
        var client = WithMock(mock).CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-User", "mikko.nieminen@kojamo.fi");

        var response = await client.GetAsync($"/api/customers?{queryString}");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Null(mock.LastQuery);
    }

    [Fact]
    public async Task GetCustomers_WithoutFilters_PassesNullsThrough()
    {
        var mock = new RecordingService { Result = Empty() };
        var client = WithMock(mock).CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-User", "mikko.nieminen@kojamo.fi");

        var response = await client.GetAsync("/api/customers");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(mock.LastQuery);
        Assert.Null(mock.LastQuery.Search);
        Assert.Null(mock.LastQuery.Type);
        Assert.Null(mock.LastQuery.Relation);
        Assert.Null(mock.LastQuery.City);
    }

    [Theory]
    [InlineData(CustomerTypes.Person)]
    [InlineData(CustomerTypes.Company)]
    [InlineData(CustomerTypes.ContactPerson)]
    public async Task GetCustomers_AcceptsAllValidTypes(string type)
    {
        var mock = new RecordingService { Result = Empty() };
        var client = WithMock(mock).CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-User", "mikko.nieminen@kojamo.fi");

        var response = await client.GetAsync($"/api/customers?type={type}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(type, mock.LastQuery?.Type);
    }

    [Theory]
    [InlineData(CustomerRelationFilters.HasContract)]
    [InlineData(CustomerRelationFilters.HasApplication)]
    [InlineData(CustomerRelationFilters.HasOffer)]
    [InlineData(CustomerRelationFilters.HasShowing)]
    [InlineData(CustomerRelationFilters.NoRelations)]
    public async Task GetCustomers_AcceptsAllValidRelations(string relation)
    {
        var mock = new RecordingService { Result = Empty() };
        var client = WithMock(mock).CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-User", "mikko.nieminen@kojamo.fi");

        var response = await client.GetAsync($"/api/customers?relation={relation}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(relation, mock.LastQuery?.Relation);
    }

    [Fact]
    public async Task GetCustomers_PassesSearchAndCity()
    {
        var mock = new RecordingService { Result = Empty() };
        var client = WithMock(mock).CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-User", "mikko.nieminen@kojamo.fi");

        var response = await client.GetAsync("/api/customers?q=manner&city=Helsinki");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("manner", mock.LastQuery?.Search);
        Assert.Equal("Helsinki", mock.LastQuery?.City);
    }

    [Fact]
    public async Task GetCustomers_ReturnsPayloadShape()
    {
        var personRow = new CustomerDto(
            Id: "c-001",
            Type: CustomerTypes.Person,
            DisplayName: "Aalto, Eero",
            Initials: "AE",
            Counts: new CustomerCountsDto(0, 0, 1, 0, 0),
            PrimaryAddress: "Mannerheimintie 12 A 4",
            City: "Helsinki",
            Tag: null,
            Phone: null,
            Email: null,
            FirstName: "Eero",
            LastName: "Aalto",
            CompanyName: null,
            BusinessId: null,
            ParentCompanyId: null,
            ParentCompanyName: null);

        var companyRow = new CustomerDto(
            Id: "c-y01",
            Type: CustomerTypes.Company,
            DisplayName: "Lumo Tekniikka Oy",
            Initials: "LT",
            Counts: new CustomerCountsDto(0, 0, 1, 0, 0),
            PrimaryAddress: "Toimitilakuja 5",
            City: "Helsinki",
            Tag: null,
            Phone: null,
            Email: null,
            FirstName: null,
            LastName: null,
            CompanyName: "Lumo Tekniikka Oy",
            BusinessId: "2345678-9",
            ParentCompanyId: null,
            ParentCompanyName: null);

        var mock = new RecordingService
        {
            Result = new CustomersResponseDto(new[] { personRow, companyRow }, 2),
        };
        var client = WithMock(mock).CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-User", "mikko.nieminen@kojamo.fi");

        var response = await client.GetAsync("/api/customers");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<CustomersResponseDto>();
        Assert.NotNull(body);
        Assert.Equal(2, body!.Total);
        Assert.Equal(2, body.Items.Count);
        Assert.Equal(CustomerTypes.Person,  body.Items[0].Type);
        Assert.Equal(CustomerTypes.Company, body.Items[1].Type);
        Assert.Equal("2345678-9",           body.Items[1].BusinessId);
    }

    // -- GET /api/customers/{id} ----------------------------------------------

    [Fact]
    public async Task GetById_WithoutToken_Returns401()
    {
        var client = WithMock(new RecordingService()).CreateClient();

        var response = await client.GetAsync("/api/customers/123");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetById_NotFound_Returns404()
    {
        var mock = new RecordingService { DetailResult = null };
        var client = WithMock(mock).CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-User", "mikko.nieminen@kojamo.fi");

        var response = await client.GetAsync("/api/customers/9999");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal(9999, mock.LastDetailId);
    }

    [Fact]
    public async Task GetById_Found_Returns200WithDto()
    {
        var personRow = new CustomerDto(
            Id: "42",
            Type: CustomerTypes.Person,
            DisplayName: "Aalto, Eero",
            Initials: "AE",
            Counts: new CustomerCountsDto(0, 0, 1, 0, 0),
            PrimaryAddress: "Mannerheimintie 12 A 4",
            City: "Helsinki",
            Tag: null,
            Phone: null,
            Email: null,
            FirstName: "Eero",
            LastName: "Aalto",
            CompanyName: null,
            BusinessId: null,
            ParentCompanyId: null,
            ParentCompanyName: null);
        var mock = new RecordingService { DetailResult = personRow };
        var client = WithMock(mock).CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-User", "mikko.nieminen@kojamo.fi");

        var response = await client.GetAsync("/api/customers/42");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<CustomerDto>();
        Assert.NotNull(body);
        Assert.Equal("42",      body!.Id);
        Assert.Equal("Aalto, Eero", body.DisplayName);
        Assert.Equal(42, mock.LastDetailId);
    }

    [Theory]
    [InlineData("abc")]
    [InlineData("0.5")]
    public async Task GetById_NonIntegerId_Returns404(string id)
    {
        var mock = new RecordingService();
        var client = WithMock(mock).CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-User", "mikko.nieminen@kojamo.fi");

        var response = await client.GetAsync($"/api/customers/{id}");

        // The {id:int} route constraint causes the endpoint to be unmatched,
        // which Minimal API returns as 404 Not Found rather than 400.
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Null(mock.LastDetailId);
    }

    private static CustomersResponseDto Empty() =>
        new(Array.Empty<CustomerDto>(), 0);

    private WebApplicationFactory<Program> WithMock(ICustomerQueryService mock) =>
        _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureTestServices(services =>
            {
                services.Replace(ServiceDescriptor.Scoped(_ => mock));
            });
        });

    private sealed class RecordingService : ICustomerQueryService
    {
        public CustomersResponseDto Result { get; set; } =
            new(Array.Empty<CustomerDto>(), 0);

        public CustomerDto? DetailResult { get; set; }

        public CustomerQueryParameters? LastQuery { get; private set; }
        public int? LastDetailId { get; private set; }

        public Task<CustomersResponseDto> ListAsync(
            ClaimsPrincipal principal,
            CustomerQueryParameters query,
            CancellationToken cancellationToken = default)
        {
            LastQuery = query;
            return Task.FromResult(Result);
        }

        public Task<CustomerDto?> GetAsync(
            ClaimsPrincipal principal,
            int asiakasNumero,
            CancellationToken cancellationToken = default)
        {
            LastDetailId = asiakasNumero;
            return Task.FromResult(DetailResult);
        }
    }
}
