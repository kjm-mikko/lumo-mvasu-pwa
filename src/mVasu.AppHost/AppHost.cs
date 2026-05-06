// .NET Aspire orchestrator for the Lumo mVasu development environment.
//
// Boots the API and the Angular PWA together with one command. The
// dashboard at https://localhost:17216 (default Aspire port) shows
// stdout, OpenTelemetry traces, and resource health for both services
// and surfaces a single click-through to each running endpoint.
//
// Run with: `dotnet run --project src/mVasu.AppHost`
//
// SQL: the API still owns its xVasu connection string (XPO needs the
// XAF-shaped configuration on the API's own user secrets). Aspire does
// not register or inject the connection here — adding it as a resource
// would make the dashboard prompt for a value it doesn't actually use.

var builder = DistributedApplication.CreateBuilder(args);

// -- mVasu.Api -------------------------------------------------------------

// Use the existing `https` launch profile so the API keeps listening on
// 7216 — the PWA's environment.apiBaseUrl is hard-coded to that port,
// and our Azure AD app registration's reply URL is rooted there. If we
// let Aspire allocate a random port the PWA would need a runtime
// service-discovery shim (see docs/decisions for the deferred decision).
var api = builder.AddProject<Projects.mVasu_Api>("api")
    .WithHttpHealthCheck("/api/health");

// -- mVasu.Pwa (Angular) ---------------------------------------------------

// `AddNodeApp` + `WithNpm()` + `WithRunScript("start")` is the Aspire 13
// shape for the legacy `AddNpmApp`. The package.json `start` script
// already pins `--ssl --host=localhost --port=4200`, so the PWA stays
// reachable at https://localhost:4200 — the URL that's registered as
// the MSAL redirect in Azure AD. We tell Aspire about the endpoint
// (so the dashboard can link to it) but mark it `isProxied: false`
// because Angular's dev-server binds the port directly and Aspire's
// reverse proxy would conflict with the SSL handshake.
//
// `BROWSER=none` keeps `ng serve` from auto-opening a tab on every
// AppHost restart — the Aspire dashboard is the single launch point.
//
// The scriptPath argument is unused once WithRunScript is set, but
// AddNodeApp's signature requires it; pass package.json as a stable
// stand-in.
var pwa = builder.AddNodeApp("pwa", "../mVasu.Pwa", "package.json")
    .WithNpm()
    .WithRunScript("start")
    .WithReference(api)
    .WaitFor(api)
    .WithHttpsEndpoint(port: 4200, targetPort: 4200, isProxied: false)
    .WithEnvironment("BROWSER", "none");

builder.Build().Run();
