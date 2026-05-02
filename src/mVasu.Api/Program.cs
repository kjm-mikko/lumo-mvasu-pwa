using System.Reflection;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Identity.Web;
using mVasu.Api.Authentication;
using mVasu.Api.Contracts;
using mVasu.Api.Data;
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

builder.Services.AddScoped<IUserResolver, StaticTestUserResolver>();

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
