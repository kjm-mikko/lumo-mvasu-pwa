using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using mVasu.Api.Contracts;
using mVasu.Api.Tasks;

namespace mVasu.Api.Tests;

public class TasksEndpointTests : IClassFixture<AuthenticatedWebApplicationFactory>
{
    private readonly AuthenticatedWebApplicationFactory _factory;

    public TasksEndpointTests(AuthenticatedWebApplicationFactory factory) => _factory = factory;

    [Fact]
    public async Task GetTasks_WithoutToken_Returns401()
    {
        var client = WithMock(new RecordingService()).CreateClient();

        var response = await client.GetAsync("/api/tasks");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetTasks_AuthenticatedWithoutScope_Returns403()
    {
        var client = WithMock(new RecordingService()).CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-User", "mikko.nieminen@kojamo.fi");
        client.DefaultRequestHeaders.Add("X-Test-NoScope", "1");

        var response = await client.GetAsync("/api/tasks");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task GetTasks_FromAfterTo_Returns400()
    {
        var mock = new RecordingService();
        var client = WithMock(mock).CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-User", "mikko.nieminen@kojamo.fi");

        var response = await client.GetAsync("/api/tasks?from=2026-06-01&to=2026-05-01");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Null(mock.LastQuery);
    }

    [Fact]
    public async Task GetTasks_DefaultsUserToMe()
    {
        var mock = new RecordingService
        {
            Result = Empty(),
        };
        var client = WithMock(mock).CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-User", "mikko.nieminen@kojamo.fi");

        var response = await client.GetAsync("/api/tasks");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(mock.LastQuery);
        Assert.Equal("me", mock.LastQuery.UserId);
        Assert.False(mock.LastQuery.UrgentOnly);
        Assert.Null(mock.LastQuery.Types);
    }

    [Fact]
    public async Task GetTasks_PassesTypesFilter()
    {
        var mock = new RecordingService
        {
            Result = Empty(),
        };
        var client = WithMock(mock).CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-User", "mikko.nieminen@kojamo.fi");

        var response = await client.GetAsync(
            "/api/tasks?types=visit-introduction,signature-pending&urgentOnly=true");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(mock.LastQuery);
        Assert.True(mock.LastQuery.UrgentOnly);
        Assert.NotNull(mock.LastQuery.Types);
        Assert.Equal(2, mock.LastQuery.Types.Count);
        Assert.Contains(TaskTypeNames.VisitIntroduction, mock.LastQuery.Types);
        Assert.Contains(TaskTypeNames.SignaturePending, mock.LastQuery.Types);
    }

    [Fact]
    public async Task GetTasks_ReturnsPayloadShape()
    {
        var mock = new RecordingService
        {
            Result = new TasksResponseDto(
                Groups: new[]
                {
                    new TaskGroupDto(
                        TaskGroupIds.Today, "Tänään", "la 4.5.",
                        new[]
                        {
                            new TaskDto(
                                Id: "t-1",
                                Type: TaskTypeNames.VisitIntroduction,
                                Accent: TaskAccents.Navy,
                                Urgent: false,
                                When: new TaskWhenDto("10:30", null, 0),
                                Title: "Mannerheimintie 12 A 4",
                                Who: "Asiakas_001",
                                Meta: null,
                                TypeLabel: null,
                                Actions: new[]
                                {
                                    new TaskActionDto(
                                        TaskActionKinds.Navigate, "Avaa kohde",
                                        Primary: true, Destructive: false, null),
                                },
                                EntityRef: new TaskEntityRefDto("core", "Tutustumiskaynti", "x-1"),
                                SortOrder: 0),
                        }),
                },
                Total: 1,
                GeneratedAt: DateTimeOffset.UtcNow),
        };
        var client = WithMock(mock).CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-User", "mikko.nieminen@kojamo.fi");

        var response = await client.GetAsync("/api/tasks");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<TasksResponseDto>();
        Assert.NotNull(body);
        Assert.Equal(1, body!.Total);
        Assert.Single(body.Groups);
        Assert.Equal(TaskGroupIds.Today, body.Groups[0].Id);
        Assert.Single(body.Groups[0].Tasks);
        Assert.Equal(TaskTypeNames.VisitIntroduction, body.Groups[0].Tasks[0].Type);
        Assert.Equal("Mannerheimintie 12 A 4", body.Groups[0].Tasks[0].Title);
    }

    private static TasksResponseDto Empty() =>
        new(Array.Empty<TaskGroupDto>(), 0, DateTimeOffset.UtcNow);

    private WebApplicationFactory<Program> WithMock(ITaskQueryService mock) =>
        _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureTestServices(services =>
            {
                services.Replace(ServiceDescriptor.Scoped(_ => mock));
            });
        });

    private sealed class RecordingService : ITaskQueryService
    {
        public TasksResponseDto Result { get; set; } =
            new(Array.Empty<TaskGroupDto>(), 0, DateTimeOffset.UtcNow);

        public TaskQueryParameters? LastQuery { get; private set; }

        public Task<TasksResponseDto> ListAsync(
            ClaimsPrincipal principal,
            TaskQueryParameters query,
            CancellationToken cancellationToken = default)
        {
            LastQuery = query;
            return Task.FromResult(Result);
        }
    }
}
