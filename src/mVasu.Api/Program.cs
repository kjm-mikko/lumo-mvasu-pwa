using System.Reflection;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Identity.Web;
using mVasu.Api.Authentication;
using mVasu.Api.Contracts;
using mVasu.Api.Data;
using mVasu.Api.Customers;
using mVasu.Api.Search;
using mVasu.Api.Tasks;
using mVasu.Api.Tiskilista;
using Scalar.AspNetCore;
using Serilog;
using Serilog.Events;

var builder = WebApplication.CreateBuilder(args);

// Aspire wiring — registers OpenTelemetry, service discovery, default
// resilience, and the standard /health + /alive probes used by the
// AppHost's `WithHttpHealthCheck` call. Safe to run outside Aspire too;
// when launched via `dotnet run` directly it just adds telemetry plumbing.
builder.AddServiceDefaults();

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

// Dev-only authentication bypass. When enabled, requests carrying an
// X-Dev-User: <email> header are authenticated as that email without an
// Azure AD JWT — used by the PWA's dev user picker (environment.devAuth)
// to switch between mVasu users for PermissionPolicy testing without
// re-logging into Microsoft. Gated at startup on Development env *and*
// the explicit config flag; production never registers the scheme.
var devHeaderAuthEnabled = builder.Environment.IsDevelopment()
    && builder.Configuration.GetValue<bool>("Development:DevHeaderAuth:Enabled");

const string CombinedAuthScheme = "Default";
var defaultAuthScheme = devHeaderAuthEnabled
    ? CombinedAuthScheme
    : JwtBearerDefaults.AuthenticationScheme;

var authBuilder = builder.Services.AddAuthentication(defaultAuthScheme);

if (devHeaderAuthEnabled)
{
    // Per-request switch: presence of X-Dev-User → dev handler, otherwise
    // standard JWT bearer. Both paths land on AccessAsUserPolicy, which
    // checks the `scp` claim — the dev handler emits `access_as_user` to
    // match.
    authBuilder.AddPolicyScheme(CombinedAuthScheme, displayName: "Auto", options =>
    {
        options.ForwardDefaultSelector = context =>
            context.Request.Headers.ContainsKey(DevHeaderAuthenticationDefaults.HeaderName)
                ? DevHeaderAuthenticationDefaults.Scheme
                : JwtBearerDefaults.AuthenticationScheme;
    });
    authBuilder.AddScheme<DevHeaderAuthenticationOptions, DevHeaderAuthenticationHandler>(
        DevHeaderAuthenticationDefaults.Scheme,
        options => options.Scope = builder.Configuration["AzureAd:Scopes"] ?? "access_as_user");
}

authBuilder.AddMicrosoftIdentityWebApi(builder.Configuration.GetSection("AzureAd"));

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
builder.Services.AddScoped<ICustomerQueryService, XpoCustomerQueryService>();
// Singleton — pure logic over the resolved xVasuSecuritySystemUser, no
// per-request state. Walks PermissionPolicy roles defensively to nullify
// customer DTO fields the caller cannot read (B2 cut, see
// CustomerFieldAccessPolicy XML doc for the rationale).
builder.Services.AddSingleton<CustomerFieldAccessPolicy>();
// Singleton — derived from xVasu.Module attributes that only change on
// NuGet upgrade. No DB / DI dependencies, safe to construct at boot.
builder.Services.AddSingleton<CustomerMetadataService>();
builder.Services.AddScoped<ISearchService, SearchService>();

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

// Development-only user impersonation. When the configuration entry
// `Development:ImpersonateEmail` is set in a Development environment,
// the EmailResolver returns that email for every authenticated request
// regardless of the principal's claims — used to log in via the normal
// Azure AD flow and then test the back-end as a different mVasu user
// (different roles, different PermissionPolicy). Production environments
// are filtered out at the env check; the setter does nothing when env
// isn't Development.
if (app.Environment.IsDevelopment())
{
    var impersonate = app.Configuration["Development:ImpersonateEmail"];
    if (!string.IsNullOrWhiteSpace(impersonate))
    {
        EmailResolver.SetDevelopmentImpersonation(impersonate);
        // Surface this loudly. A silent impersonation that survives a
        // forgotten config edit is exactly the bug we don't want.
        app.Logger.LogWarning(
            "DEV IMPERSONATION ACTIVE: every authenticated request will resolve to {Email}. " +
            "Disable by clearing Development:ImpersonateEmail or unsetting the env var Development__ImpersonateEmail.",
            impersonate);
    }

    if (devHeaderAuthEnabled)
    {
        app.Logger.LogWarning(
            "DEV HEADER AUTH ACTIVE: requests with header {Header}: <email> authenticate without an Azure AD JWT. " +
            "Disable by setting Development:DevHeaderAuth:Enabled=false (or unsetting the env var " +
            "Development__DevHeaderAuth__Enabled). Production registrations are gated by IsDevelopment().",
            DevHeaderAuthenticationDefaults.HeaderName);
    }
}

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

// Aspire convention endpoints — `/health` (full check pipeline) and
// `/alive` (liveness only). These complement the existing `/api/health`
// route the dashboard uses for its WithHttpHealthCheck probe.
app.MapDefaultEndpoints();

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
        string? laji,
        string? tyyppi,
        string? kunta,
        string? kaupunginosa,
        string? sopimustila,
        string? isannoitsija,
        string? markkinoija,
        float? neliotMin,
        float? neliotMax,
        DateOnly? vapautuuFrom,
        DateOnly? vapautuuTo,
        bool? onKuvausTarveOnly,
        bool? lumoFiOnly,
        bool? hasUpcomingEsittelyOnly,
        string? scope,
        string? sortBy,
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

        // Range bounds — bail rather than silently returning empty set.
        if (neliotMin is { } nmin && neliotMax is { } nmax && nmin > nmax)
        {
            errors[nameof(neliotMin)] = ["neliotMin must be less than or equal to neliotMax."];
        }
        if (vapautuuFrom is { } vff && vapautuuTo is { } vft && vff > vft)
        {
            errors[nameof(vapautuuFrom)] = ["vapautuuFrom must be on or before vapautuuTo."];
        }

        if (errors.Count > 0)
        {
            return Results.ValidationProblem(errors);
        }

        static string[]? CommaList(string? raw) => string.IsNullOrWhiteSpace(raw)
            ? null
            : raw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        var query = new TiskilistaListQuery(
            Search: q,
            Status: status,
            Lajit: CommaList(laji),
            Tyypit: CommaList(tyyppi),
            Kunnat: CommaList(kunta),
            Kaupunginosat: CommaList(kaupunginosa),
            Sopimustilat: CommaList(sopimustila),
            Isannoitsijat: CommaList(isannoitsija),
            Markkinoijat: CommaList(markkinoija),
            NeliotMin: neliotMin,
            NeliotMax: neliotMax,
            VapautuuFrom: vapautuuFrom,
            VapautuuTo: vapautuuTo,
            OnKuvausTarveOnly: onKuvausTarveOnly,
            LumoFiOnly: lumoFiOnly,
            HasUpcomingEsittelyOnly: hasUpcomingEsittelyOnly,
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

app.MapGet("/api/tiskilista/distinct-values", async (
        ClaimsPrincipal user,
        ITiskilistaQueryService service,
        CancellationToken ct) =>
    {
        var values = await service.GetDistinctValuesAsync(user, ct);
        return values is null
            ? Results.Problem(
                title: "User not provisioned",
                detail: "Authenticated principal could not be resolved to a Lumo mVasu user.",
                statusCode: StatusCodes.Status403Forbidden)
            : Results.Ok(values);
    })
    .WithName("GetTiskilistaDistinctValues")
    .WithSummary("Returns sorted distinct dimensions (laji, tyyppi, kunta, kaupunginosa, sopimustila, isannoitsija, tila) " +
                 "scoped to the user's BranchCode visibility — used to populate filter dropdowns.")
    .RequireAuthorization(AccessAsUserPolicy);

app.MapGet("/api/tiskilista/{id:guid}", async (
        Guid id,
        ClaimsPrincipal user,
        ITiskilistaQueryService service,
        double? userLat,
        double? userLon,
        CancellationToken ct) =>
    {
        var detail = await service.GetAsync(user, id, userLat, userLon, ct);
        return detail is null
            ? Results.NotFound()
            : Results.Ok(detail);
    })
    .WithName("GetTiskilistaById")
    .WithSummary("Returns the full Tiskilista detail by Oid. " +
                 "Optional userLat/userLon enable distance computation matching the list view.")
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

app.MapGet("/api/customers", async (
        ClaimsPrincipal user,
        ICustomerQueryService service,
        string? q,
        string? type,
        string? relation,
        string? city,
        CancellationToken ct) =>
    {
        var errors = new Dictionary<string, string[]>();

        if (!string.IsNullOrWhiteSpace(type) && !CustomerTypes.Valid.Contains(type))
        {
            errors[nameof(type)] = [$"type must be one of: {string.Join(", ", CustomerTypes.Valid)}."];
        }

        if (!string.IsNullOrWhiteSpace(relation) && !CustomerRelationFilters.Valid.Contains(relation))
        {
            errors[nameof(relation)] =
                [$"relation must be one of: {string.Join(", ", CustomerRelationFilters.Valid)}."];
        }

        if (errors.Count > 0)
        {
            return Results.ValidationProblem(errors);
        }

        var query = new CustomerQueryParameters(
            Search: q,
            Type: string.IsNullOrWhiteSpace(type) ? null : type,
            Relation: string.IsNullOrWhiteSpace(relation) ? null : relation,
            City: string.IsNullOrWhiteSpace(city) ? null : city);

        var result = await service.ListAsync(user, query, ct);
        return Results.Ok(result);
    })
    .WithName("GetCustomers")
    .WithSummary("Lists Asiakkaat (Henkilö / Yritys / Yhteyshenkilö) sorted fi-FI " +
                 "by displayName. Projects from xVasu.Data.Asma.Asiakas with related-" +
                 "entity counts (Hakemus, SopimusVaraus, Sopimus, Tutustumiskäynti). " +
                 "Field-level read access enforced defensively against the caller's " +
                 "PermissionPolicy roles — see CustomerFieldAccessPolicy.")
    .RequireAuthorization(AccessAsUserPolicy);

app.MapGet("/api/customers/{id:int}", async (
        ClaimsPrincipal user,
        ICustomerQueryService service,
        int id,
        CancellationToken ct) =>
    {
        var dto = await service.GetAsync(user, id, ct);
        return dto is null ? Results.NotFound() : Results.Ok(dto);
    })
    .WithName("GetCustomerById")
    .WithSummary("Returns the full Asiakas row by AsiakasNumero. 404 when " +
                 "the row doesn't exist or the caller lacks XPO permission to see it. " +
                 "Field-level read access is enforced via CustomerFieldAccessPolicy " +
                 "— denied properties come back null in the DTO.")
    .RequireAuthorization(AccessAsUserPolicy);

app.MapGet("/api/customers/metadata", (CustomerMetadataService metadata) =>
        Results.Ok(metadata.GetMetadata()))
    .WithName("GetCustomerMetadata")
    .WithSummary("XAF / Model.xafml metadata for Henkilö / Yritys / Yhteyshenkilö " +
                 "detail views — required, read-only, maxLength, mask and class-level " +
                 "appearance rules. Cached per process; static across requests.")
    .RequireAuthorization(AccessAsUserPolicy);

app.MapGet("/api/search", async (
        ClaimsPrincipal user,
        ISearchService service,
        string? q,
        int? limit,
        CancellationToken ct) =>
    {
        var resolvedLimit = limit ?? 5;
        if (resolvedLimit < 1 || resolvedLimit > 50)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                [nameof(limit)] = ["limit must be between 1 and 50."],
            });
        }

        var query = new SearchQueryParameters(Q: q, Limit: resolvedLimit);
        var result = await service.SearchAsync(user, query, ct);
        return Results.Ok(result);
    })
    .WithName("Search")
    .WithSummary("Long-tail global search across XAF entity tables (units, people, " +
                 "contracts) and the static action catalogue. Min query length 2; " +
                 "shorter inputs return an empty group list. Phase 1 runs against " +
                 "the local mock fixture; Phase 2 will use Postgres tsvector / " +
                 "pg_trgm fuzzy match.")
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
