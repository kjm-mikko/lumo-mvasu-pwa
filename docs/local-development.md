# Local development

Step-by-step setup for running the Lumo mVasu PWA aihio on your own machine.

## Prerequisites

| Tool | Version | Purpose |
| --- | --- | --- |
| .NET SDK | 10.0.103 (locked in `global.json`) | Backend |
| Node.js | ≥ 22 LTS | PWA |
| npm | ≥ 11 | PWA packages |
| SQL Server | dev instance you own | XPO data |
| Visual Studio Code | latest | IDE — `.vscode/extensions.json` lists the recommended set |

## One-time setup

### 1. Trust the dev HTTPS certificate

Both the API (`https://localhost:7216`) and the PWA (`https://localhost:4200`)
serve over HTTPS. Run once per machine:

```powershell
dotnet dev-certs https --trust
```

Optional — if you want Angular's `ng serve --ssl` to use the same trusted cert
instead of its self-signed default:

```powershell
dotnet dev-certs https --export-path "$env:USERPROFILE\.aspnet\https\localhost.pem" --format Pem --no-password
```

Then update `src/mVasu.Pwa/package.json` `start` script with `--ssl-key=` and
`--ssl-cert=` flags pointing to the exported files.

### 2. Configure the database connection string

Apply the schema scripts to your dev database first — see
[`db/scripts/README.md`](../db/scripts/README.md) for the current set. Then put
the connection string in user secrets so it never lands in the repo:

```powershell
cd src/mVasu.Api
dotnet user-secrets init
dotnet user-secrets set "ConnectionStrings:VasuDb" "<your dev connection string>"
```

### 3. Authenticate against the Azure DevOps NuGet feed

`xVasu.*` packages live in the private `kjm-sk-ado` feed. Install the Azure
Artifacts Credential Provider once:

```powershell
iex "& { $(irm https://aka.ms/install-artifacts-credprovider.ps1) }"
```

The first `dotnet restore` triggers a Microsoft sign-in — use your work account.
Tokens cache locally afterwards.

### 4. Install PWA dependencies

```powershell
npm install --prefix src/mVasu.Pwa
```

`npm` resolves DevExtreme, MSAL, Angular and font packages from npmjs.org. The
private feed is .NET-only.

## Daily workflow

### Run the backend

```powershell
dotnet watch run --project src/mVasu.Api
```

or `F5` in VS Code → `Launch API`. Scalar UI opens automatically at
`https://localhost:7216/scalar/v1`.

### Run the PWA

```powershell
npm start --prefix src/mVasu.Pwa
```

Browser opens at `https://localhost:4200`. Accept the dev cert prompt the first
time (or skip the prompt by exporting the dotnet dev cert as described above).
PWA hot-reloads on file changes.

### Run both at once

`F5` → `Launch Full Stack` (the compound configuration in
[`.vscode/launch.json`](../.vscode/launch.json)) starts API + PWA together.

### Run tests

```powershell
dotnet test                              # backend integration tests (xUnit)
npm test --prefix src/mVasu.Pwa -- --watch=false   # frontend unit tests (Karma + Jasmine)
```

The xUnit suite uses `WebApplicationFactory<Program>` with a mocked
`IUserResolver` / `IUserSettingsService` / `IDbHealthCheck`, so it does not need
a database to run.

### Build for production

```powershell
dotnet build Lumo.mVasu.slnx -c Release
npm run build --prefix src/mVasu.Pwa
```

PWA output lands in `src/mVasu.Pwa/dist/m-vasu.pwa/`. Initial bundle size sits
around 1.4 MB raw / ~210 kB transferred — most of it is DevExtreme stock CSS
and the two variable font families. Service worker activates only in production
builds and caches everything in the `dist` folder.

## Verifying the auth flow

Once you have signed in to the PWA:

1. `/home` → header shows your display name, greeting matches local time.
2. `/settings` → profile card shows your `displayName` + email from XPO.
3. Edit "Mieluisin nimi" → Save → `/home` greeting updates.
4. Toggle location consent ON → browser prompts for location → status changes
   to "Sallittu" and a row lands in `MVasuUserSettings` (verify via SQL).
5. Open the API directly: `https://localhost:7216/api/health` → 200 OK,
   `https://localhost:7216/api/health/db` → 200 OK if the DB is reachable.

## Troubleshooting

**`NU1900: Error occurred while getting package vulnerability data`** — credential
provider has not signed in yet. Run `dotnet restore --interactive` once and
follow the device-code prompt.

**`NU1101: Unable to find package xVasu.*`** — the `kjm-sk-ado` feed URL got
out of sync, or you logged in to the wrong tenant. Re-run
`dotnet restore --interactive`. The feed URL is in `nuget.config` at the repo
root.

**`The "xVasu.Data.Security.xVasuSecuritySystemUser" class is not registered…`** —
the headless XafApplication did not finish `Setup()`. Confirm the connection
string is valid and that the database is reachable.

**`SchemaCorrectionNeededException: Table 'X' not found`** — an `xVasu` table
your call path traverses is missing from your dev DB. Either use
`AutoCreateOption.SchemaAlreadyExists` (the default we ship) and ignore the
table, or run the missing legacy script. Application code never adds DDL.

**`Object reference not set` from `ModelNavigationItemsDomainLogic`** — XAF
tried to build a UI navigation tree we do not have. The DI factory in
`XpoServiceCollectionExtensions` swallows this NRE deliberately; if it now
escapes, check whether someone removed the catch.

**Browser says "Your connection is not private" on `https://localhost:4200`** —
the Angular dev server uses a self-signed cert. Hit "Advanced" → "Proceed", or
export the dotnet dev cert and configure `ng serve --ssl-key` / `--ssl-cert`.

## Branches and commits

- Releases live on `main`.
- Integration work targets `develop`.
- Each unit of work is a `feature/<topic>` branch off `develop`, merged back
  through a PR.
- Commit messages follow [Conventional Commits](https://www.conventionalcommits.org):
  `feat(api): …`, `fix(pwa): …`, `chore(deps): …`, `docs: …`. Multi-line body
  is encouraged for non-trivial changes.
