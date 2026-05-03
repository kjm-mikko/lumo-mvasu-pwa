using System.Reflection;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Identity.Web;
using mVasu.Api.Authentication;
using mVasu.Api.Contracts;
using mVasu.Api.Data;
using mVasu.Api.Tasks;
using mVasu.Api.Tiskilista;
using Scalar.AspNetCore;
using Serilog;
using Serilog.Events;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((context, services, configuration) =>
{
    configuration
        .MinimumLevel.Information()
        .MinimumLevel.Override("Microsoft.AspNetCore", LogEventLevel.Warning)
        .MinimumLevel.Override("Microsoft.Hosting.Lifetime", LogEventLevel.Information)
        .MinimumLevel.Override("System", LogEventLevel.Warning)
        .Enrich.FromLogContext()
        .Enrich.WithProperty("Application", "Lumo.mVasu.Api")
        .ReadFrom.Configuration(context.Configuration)
        .ReadFrom.Services(services)
        .WriteTo.Console();

    var aiConnectionString = context.Configuration["ApplicationInsights:ConnectionString"];
    if (!string.IsNullOrWhiteSpace(aiConnectionString))
    {
        configuration.WriteTo.ApplicationInsights(aiConnectionString, TelemetryConverter.Traces);
    }
});

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddMicrosoftIdentityWebApi(builder.Configuration.GetSection("AzureAd"));

const string AccessAsUserPolicy = "AccessAsUser";
builder.Services.AddAuthorization(options =>
{
    var requiredScope = builder.Configuration["AzureAd:Scopes"] ?? "access_as_user";
    options.AddPolicy(AccessAsUserPolicy, policy =>
    {
        policy.RequireAuthenticatedUser();
        policy.RequireAssertion(context =>
        {
            var scopeClaim = context.User.FindFirst("scp")?.Value
                ?? context.User.FindFirst("http://schemas.microsoft.com/identity/claims/scope")?.Value;
            return scopeClaim?.Split(' ').Contains(requiredScope) == true;
        });
    });
});

builder.Services.AddScoped<IUserResolver, XpoEmailUserResolver>();
builder.Services.AddScoped<IUserSettingsService, XpoUserSettingsService>();
builder.Services.AddScoped<ITiskilistaQueryService, TiskilistaQueryService>();
builder.Services.AddScoped<ITaskQueryService, TaskQueryService>();

builder.Services.AddVasuXpo(builder.Configuration);

builder.Services.AddOpenApi();

const string DevCorsPolicy = "LumoPwaDev";
var corsOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
builder.Services.AddCors(options =>
{
    options.AddPolicy(DevCorsPolicy, policy => policy
        .WithOrigins(corsOrigins)
        .AllowAnyHeader()
        .AllowAnyMethod()
        .AllowCredentials());
});

var app = builder.Build();

var startedAt = DateTimeOffset.UtcNow;
var version = Assembly.GetExecutingAssembly()
    .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
    ?? Assembly.GetExecutingAssembly().GetName().Version?.ToString()
    ?? "0.0.0";

app.UseSerilogRequestLogging();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference(options => options
        .WithTitle("Lumo mVasu API")
        .WithTheme(ScalarTheme.BluePlanet));
    app.UseCors(DevCorsPolicy);
}

app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/api/health", () => new HealthCheckDto(
        Status: "Healthy",
        Version: version,
        UptimeSeconds: (DateTimeOffset.UtcNow - startedAt).TotalSeconds,
        Timestamp: DateTimeOffset.UtcNow))
    .WithName("GetHealth")
    .WithSummary("Liveness probe — returns API version and uptime. Anonymous.")
    .AllowAnonymous();

app.MapGet("/api/health/db", async (IDbHealthCheck check, CancellationToken ct) =>
    {
        var result = await check.CheckAsync(ct);
        return result.Connected
            ? Results.Ok(result)
            : Results.Json(result, statusCode: StatusCodes.Status503ServiceUnavailable);
    })
    .WithName("GetDbHealth")
    .WithSummary("Verifies the XPO data layer can open a connection. Anonymous.")
    .AllowAnonymous();

app.MapGet("/api/me", async (ClaimsPrincipal user, IUserResolver resolver, CancellationToken ct) =>
    {
        var profile = await resolver.ResolveAsync(user, ct);
        return profile is null
            ? Results.Problem(
                title: "User not provisioned",
                detail: "Authenticated principal could not be resolved to a Lumo mVasu user.",
                statusCode: StatusCodes.Status403Forbidden)
            : Results.Ok(profile);
    })
    .WithName("GetCurrentUser")
    .WithSummary("Returns the authenticated user's mVasu profile.")
    .RequireAuthorization(AccessAsUserPolicy);

string[] validThemes = ["light", "dark", "system"];
string[] validLanguages = ["fi", "en"];
const int preferredNameMaxLength = 100;

app.MapPut("/api/me/settings", async (
        UpdateSettingsDto dto,
        ClaimsPrincipal user,
        IUserSettingsService settings,
        CancellationToken ct) =>
    {
        var errors = new Dictionary<string, string[]>();

        if (string.IsNullOrWhiteSpace(dto.Theme) || !validThemes.Contains(dto.Theme))
        {
            errors[nameof(dto.Theme)] = [$"Theme must be one of: {string.Join(", ", validThemes)}."];
        }

        if (string.IsNullOrWhiteSpace(dto.Language) || !validLanguages.Contains(dto.Language))
        {
            errors[nameof(dto.Language)] = [$"Language must be one of: {string.Join(", ", validLanguages)}."];
        }

        if (dto.PreferredName is { Length: > preferredNameMaxLength })
        {
            errors[nameof(dto.PreferredName)] = [$"PreferredName must be at most {preferredNameMaxLength} characters."];
        }

        if (errors.Count > 0)
        {
            return Results.ValidationProblem(errors);
        }

        var profile = await settings.UpdateAsync(user, dto, ct);
        return profile is null
            ? Results.Problem(
                title: "User not provisioned",
                detail: "Authenticated principal could not be resolved to a Lumo mVasu user.",
                statusCode: StatusCodes.Status403Forbidden)
            : Results.Ok(profile);
    })
    .WithName("UpdateCurrentUserSettings")
    .WithSummary("Updates PreferredName, Theme and Language for the authenticated user.")
    .RequireAuthorization(AccessAsUserPolicy);

app.MapPut("/api/me/location-consent", async (
        UpdateLocationConsentDto dto,
        ClaimsPrincipal user,
        IUserSettingsService settings,
        CancellationToken ct) =>
    {
        var profile = await settings.UpdateLocationConsentAsync(user, dto.Consent, ct);
        return profile is null
            ? Results.Problem(
                title: "User not provisioned",
                detail: "Authenticated principal could not be resolved to a Lumo mVasu user.",
                statusCode: StatusCodes.Status403Forbidden)
            : Results.Ok(profile);
    })
    .WithName("UpdateCurrentUserLocationConsent")
    .WithSummary("Sets the user's consent flag for storing location data. Revoking clears the last known location.")
    .RequireAuthorization(AccessAsUserPolicy);

app.MapPost("/api/me/location", async (
        UserLocationDto dto,
        ClaimsPrincipal user,
        IUserSettingsService settings,
        TimeProvider clock,
        CancellationToken ct) =>
    {
        var errors = new Dictionary<string, string[]>();

        if (dto.Latitude is < -90 or > 90 || double.IsNaN(dto.Latitude))
        {
            errors[nameof(dto.Latitude)] = ["Latitude must be between -90 and 90."];
        }

        if (dto.Longitude is < -180 or > 180 || double.IsNaN(dto.Longitude))
        {
            errors[nameof(dto.Longitude)] = ["Longitude must be between -180 and 180."];
        }

        if (dto.Accuracy is < 0 || (dto.Accuracy is { } a && double.IsNaN(a)))
        {
            errors[nameof(dto.Accuracy)] = ["Accuracy must be non-negative when provided."];
        }

        if (dto.RecordedAt > clock.GetUtcNow().AddMinutes(5))
        {
            errors[nameof(dto.RecordedAt)] = ["RecordedAt cannot be in the future."];
        }

        if (errors.Count > 0)
        {
            return Results.ValidationProblem(errors);
        }

        var result = await settings.RecordLocationAsync(user, dto, ct);
        return result switch
        {
            RecordLocationResult.Recorded => Results.NoContent(),
            RecordLocationResult.ConsentNotGranted => Results.Problem(
                title: "Location consent not granted",
                detail: "The user has not granted consent to store location data.",
                statusCode: StatusCodes.Status403Forbidden),
            _ => Results.Problem(
                title: "User not provisioned",
                detail: "Authenticated principal could not be resolved to a Lumo mVasu user.",
                statusCode: StatusCodes.Status403Forbidden),
        };
    })
    .WithName("RecordCurrentUserLocation")
    .WithSummary("Stores the user's latest known location. Requires location consent to be granted.")
    .RequireAuthorization(AccessAsUserPolicy);

app.MapGet("/api/tiskilista", async (
        ClaimsPrincipal user,
        ITiskilistaQueryService service,
        string? q,
        string? status,
        string scope,
        string sortBy,
        double? userLat,
        double? userLon,
        int? page,
        int? pageSize,
        CancellationToken ct) =>
    {
        var errors = new Dictionary<string, string[]>();

        var resolvedScope = string.IsNullOrWhiteSpace(scope) ? "omat" : scope;
        if (!TiskilistaListQuery.ValidScopes.Contains(resolvedScope))
        {
            errors[nameof(scope)] = [$"scope must be one of: {string.Join(", ", TiskilistaListQuery.ValidScopes)}."];
        }

        var resolvedSort = string.IsNullOrWhiteSpace(sortBy) ? "vapautuu" : sortBy;
        if (!TiskilistaListQuery.ValidSortBy.Contains(resolvedSort))
        {
            errors[nameof(sortBy)] = [$"sortBy must be one of: {string.Join(", ", TiskilistaListQuery.ValidSortBy)}."];
        }

        var resolvedPage = page ?? 1;
        if (resolvedPage < 1)
        {
            errors[nameof(page)] = ["page must be >= 1."];
        }

        var resolvedPageSize = pageSize ?? 20;
        if (resolvedPageSize < TiskilistaListQuery.MinPageSize || resolvedPageSize > TiskilistaListQuery.MaxPageSize)
        {
            errors[nameof(pageSize)] = [$"pageSize must be between {TiskilistaListQuery.MinPageSize} and {TiskilistaListQuery.MaxPageSize}."];
        }

        if (errors.Count > 0)
        {
            return Results.ValidationProblem(errors);
        }

        var query = new TiskilistaListQuery(
            Search: q,
            Status: status,
            Scope: resolvedScope,
            SortBy: resolvedSort,
            UserLat: userLat,
            UserLon: userLon,
            Page: resolvedPage,
            PageSize: resolvedPageSize);

        var result = await service.ListAsync(user, query, ct);
        return result is null
            ? Results.Problem(
                title: "User not provisioned",
                detail: "Authenticated principal could not be resolved to a Lumo mVasu user.",
                statusCode: StatusCodes.Status403Forbidden)
            : Results.Ok(result);
    })
    .WithName("GetTiskilista")
    .WithSummary("Lists Tiskilista entries filtered by scope, status and free text. Optional distance sort uses userLat/userLon.")
    .RequireAuthorization(AccessAsUserPolicy);

app.MapGet("/api/tiskilista/{id:guid}", async (
        Guid id,
        ClaimsPrincipal user,
        ITiskilistaQueryService service,
        CancellationToken ct) =>
    {
        var detail = await service.GetAsync(user, id, ct);
        return detail is null
            ? Results.NotFound()
            : Results.Ok(detail);
    })
    .WithName("GetTiskilistaById")
    .WithSummary("Returns the full Tiskilista detail by Oid.")
    .RequireAuthorization(AccessAsUserPolicy);

app.MapGet("/api/tasks", async (
        ClaimsPrincipal user,
        ITaskQueryService service,
        DateOnly? from,
        DateOnly? to,
        string? userId,
        string? types,
        bool? urgentOnly,
        CancellationToken ct) =>
    {
        var errors = new Dictionary<string, string[]>();

        if (from is not null && to is not null && from > to)
        {
            errors[nameof(from)] = ["from must be on or before to."];
        }

        var typeList = string.IsNullOrWhiteSpace(types)
            ? null
            : types.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        if (errors.Count > 0)
        {
            return Results.ValidationProblem(errors);
        }

        var query = new TaskQueryParameters(
            From: from,
            To: to,
            UserId: string.IsNullOrWhiteSpace(userId) ? "me" : userId,
            Types: typeList,
            UrgentOnly: urgentOnly ?? false);

        var result = await service.ListAsync(user, query, ct);
        return Results.Ok(result);
    })
    .WithName("GetTasks")
    .WithSummary("Aggregates the day-grouped task queue for the authenticated user. " +
                 "Phase 1 returns a fixed mock fixture; Phase 2 will run XPO queries " +
                 "across the XAF entity sources listed in BACKEND.md §2.")
    .RequireAuthorization(AccessAsUserPolicy);

app.MapGet("/api/tasks/{id}", async (
        string id,
        ClaimsPrincipal user,
        ITaskQueryService service,
        CancellationToken ct) =>
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            return Results.BadRequest();
        }

        var detail = await service.GetAsync(user, id, ct);
        return detail is null
            ? Results.NotFound()
            : Results.Ok(detail);
    })
    .WithName("GetTaskById")
    .WithSummary("Returns the full task detail for the given id. Phase 1 looks the " +
                 "row up in the mock fixture; Phase 2 will resolve the XAF entity " +
                 "via the row's EntityRef.")
    .RequireAuthorization(AccessAsUserPolicy);

try
{
    Log.Information("Lumo mVasu API starting (version {Version})", version);
    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Lumo mVasu API failed to start");
    throw;
}
finally
{
    Log.CloseAndFlush();
}

public partial class Program;
