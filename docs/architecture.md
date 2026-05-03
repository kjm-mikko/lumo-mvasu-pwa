# Architecture

High-level reference for the Lumo mVasu PWA aihio. Use it together with
[`design/open-decisions.md`](../design/open-decisions.md) — the OD entries
record _why_ each architectural choice was made and what triggers a revisit.

## At a glance

| Layer | Project | Tech |
| --- | --- | --- |
| Web API | [`src/mVasu.Api/`](../src/mVasu.Api/) | ASP.NET Core 10 minimal API, Microsoft.Identity.Web, Serilog, Scalar OpenAPI UI; domain feature folders (e.g. `Tiskilista/`) hold IService + Service + query envelope |
| Shared DTOs | [`src/mVasu.Api.Contracts/`](../src/mVasu.Api.Contracts/) | .NET 10 class library, `record` types only |
| Domain / persistence | inside `mVasu.Api`, [`Domain/`](../src/mVasu.Api/Domain/) + [`Data/`](../src/mVasu.Api/Data/) | DevExpress XAF + XPO 25.2, xVasu module |
| Schema scripts | [`db/scripts/`](../db/scripts/) | Hand-applied SQL — runtime never emits DDL |
| PWA | [`src/mVasu.Pwa/`](../src/mVasu.Pwa/) | Angular 19, DevExtreme 25.2, MSAL Angular 4, @angular/pwa |
| Tests | [`tests/mVasu.Api.Tests/`](../tests/mVasu.Api.Tests/) | xUnit, Microsoft.AspNetCore.Mvc.Testing, RecordingService stubs |

## Authentication flow

```text
PWA                                   Entra ID                         mVasu.Api
 │                                       │                                 │
 │ click "Kirjaudu Microsoft-tilillä"     │                                 │
 ├──────loginRedirect (Auth Code+PKCE)──▶│                                 │
 │                                       │  (user signs in)                │
 │◀──────redirect /auth/callback?code───┤                                 │
 │ exchange code → access_token          │                                 │
 │ store in sessionStorage               │                                 │
 │                                       │                                 │
 │ GET /api/me  Authorization: Bearer …  │                                 │
 ├───────────────────────────────────────┼────────────────────────────────▶│
 │                                       │                                 │ AuthN: validate JWT
 │                                       │                                 │ AuthZ: AccessAsUser policy
 │                                       │                                 │ resolve user via XpoEmailUserResolver
 │◀──────────────────────────────────────┼─────────── 200 UserProfileDto ──┤
```

Same App Registration backs both the SPA platform (PWA) and the API audience
(`api://d2f69daf-…`). MsalInterceptor automatically attaches the bearer token
to any request whose URL matches the configured `protectedResourceMap`.

The XPO resolver replicates the legacy mVasu rules:

- Build email variants: `<localPart>@kojamo.fi` / `@lumo.fi` / `@kojamo.onmicrosoft.com`.
- Match users where `Email` is in that list AND
  - `UserName` starts with `vvoad` (Kojamo AD), OR
  - email ends with `@kojamo.onmicrosoft.com` (B2B guest)
- AND `mVasuEnabled` AND `IsActive` in both branches.
- No JIT provisioning — unknown users get 403.

See [`src/mVasu.Api/Authentication/EmailResolver.cs`](../src/mVasu.Api/Authentication/EmailResolver.cs).

## Data flow

```text
PWA                                          mVasu.Api
 │                                              │
 │ HomeComponent.ngOnInit                       │
 │   userApi.getProfile()                       │
 ├─────────────────────────GET /api/me─────────▶│
 │                                              │ XpoEmailUserResolver:
 │                                              │  os.FindObject<xVasuSecuritySystemUser>(criteria)
 │                                              │  os.FindObject<MVasuUserSettings>(s.User.Oid == user.Oid)
 │                                              │  → UserProfileDto
 │◀─────────────────────────200 OK──────────────┤
 │ profile signal → greeting                    │
 │                                              │
 │ SettingsComponent.save()                     │
 ├──────PUT /api/me/settings { name, theme,     │
 │                              language }      │
 │                                              │ XpoUserSettingsService.UpdateAsync:
 │                                              │  validate, FindOrCreate MVasuUserSettings,
 │                                              │  patch fields, os.CommitChanges()
 │◀────────────────200 OK UserProfileDto────────┤
 │                                              │
 │ Toggle location consent ON                   │
 ├──────PUT /api/me/location-consent ───────────▶
 │◀────────────────200 OK UserProfileDto────────┤
 │ LocationService.getCurrent()                 │
 │   navigator.geolocation.getCurrentPosition   │
 ├──────POST /api/me/location ──────────────────▶
 │◀────────────────204 No Content───────────────┤
```

## XAF hosting in a Web API

The xVasu module is built on top of XAF, so its persistent classes only resolve
through `IObjectSpace.FindObject<T>` after `XafApplication.Setup()` has run and
populated `XafTypesInfo`. mVasu.Api therefore hosts a headless XafApplication:

1. [`MVasuApiModule`](../src/mVasu.Api/Domain/MVasuApiModule.cs) collects
   `AdditionalExportedTypes` for `MVasuUserSettings` plus every `PersistentBase`
   type under any `xVasu.*` namespace via assembly scan.
2. [`MVasuApiApplication`](../src/mVasu.Api/Data/MVasuApiApplication.cs)
   subclasses `XafApplication` and throws `NotSupportedException` from
   `CreateLayoutManagerCore` (Web API has no layout manager).
3. [`AddVasuXpo`](../src/mVasu.Api/Data/XpoServiceCollectionExtensions.cs) wires
   it into DI: builds the application once, hooks
   `CreateCustomObjectSpaceProvider` with our
   [`MutableSchemaDataStoreProvider`](../src/mVasu.Api/Data/MutableSchemaDataStoreProvider.cs),
   calls `Setup()` and exposes `application.ObjectSpaceProvider` to consumers.

`Setup()` succeeds for everything we need (modules, types, ObjectSpaceProvider)
and only fails when XAF tries to build the navigation/UI model — we wrap the
call in try/catch and re-throw only when the ObjectSpaceProvider was not
produced. See [OD-010](../design/open-decisions.md#od-010--xaf-hostaus-mvasuapissa-xafapplication-vs-xpo-suora)
for the alternatives we considered.

## Schema policy: manual-only DDL

The runtime is configured with `AutoCreateOption.SchemaAlreadyExists`, which
performs no schema validation or modification at all. Every table or column we
own ships as a hand-runnable SQL script under [`db/scripts/`](../db/scripts/).
Apply scripts in numeric order; the README in that folder lists the latest set.
This rule comes from a project guardrail — see
[`feedback_db_schema_manual.md`](https://github.com/) memory and
[OD-009](../design/open-decisions.md#od-009--käyttäjäasetusten-tallennusmuoto-kentät-vs-json-blob)
for the storage shape decision around `MVasuUserSettings`.

## PWA structure

```text
mVasu.Pwa/src/app
├── app.component.ts       MSAL initialize + handleRedirectObservable subscription
├── app.config.ts          MSAL providers (PublicClientApplication, GuardConfig,
│                          InterceptorConfig, MsalInterceptor in HTTP_INTERCEPTORS)
├── app.routes.ts          /auth/login, /auth/callback (root), and /home, /settings
│                          under MainLayoutComponent guarded by authGuard (= MsalGuard)
├── core/
│   ├── guards/auth.guard.ts        Delegates to MsalGuard
│   ├── services/auth.service.ts    Signal wrapper around MsalService + Broadcast
│   ├── services/user-api.service.ts HttpClient wrapper for /api/me*
│   ├── services/location.service.ts navigator.geolocation behind permission signal
│   ├── services/theme.service.ts   light / dark / system → body class + localStorage
│   ├── services/greeting.ts        time-of-day Finnish greeting (pure)
│   └── models/                     TS mirrors of Contracts records
├── layout/main-layout/    sidebar (>=768 px) + bottom tab bar (<768 px)
├── shared/wordmark/       "Lumo" wordmark, Playfair Display
├── shared/avatar/         Initials in tinted circle
├── features/login         loginRedirect CTA, gracefully redirects authenticated visits
├── features/auth-callback Listens for InteractionStatus.None / LOGIN_FAILURE → /home or /auth/login
├── features/home          Time-based greeting + "Saatavilla nyt" / "Tulossa" cards
└── features/settings      Profile card (avatar + name + email),
                           PreferredName / Language form,
                           location consent toggle wired to LocationService,
                           Theme radio bound to ThemeService,
                           Logout button
```

Styles cascade `@fontsource-variable/* → DevExtreme stock theme → design/lumo-tokens.css → design/lumo-devextreme-overrides.scss`.
The design folder is the **single source of truth**; the PWA imports it via
relative paths so token edits do not require copy-pasting.

## Domain features

### Tiskilista (D1)

`xVasu.Data.Asutus.Tiskilista` exposed through:

- `GET /api/tiskilista` — paginated list. Query parameters:
  - `q` — free text Contains across `katuosoite`, `kunta`, `KuntaAlue`, `Postitoimipaikka`
  - `status` — exact match on `Tila` ("Vapaa" / "Varattu" / null = all)
  - `scope` — `omat` (filters by user's `AlueToimistot` string list) or `kaikki`
  - `sortBy` — `vapautuu` (default), `osoite`, `vuokra` or `distance`
  - `userLat` / `userLon` — used only when `sortBy=distance`. Haversine in C# on the filtered set
  - `page`, `pageSize` (1–100)
- `GET /api/tiskilista/{id:guid}` — full detail.

The PWA renders a card grid with text search, scope chips and status chips
([`tiskilista-list.component.ts`](../src/mVasu.Pwa/src/app/features/tiskilista/tiskilista-list.component.ts)),
opens a detail view with a "Navigoi kohteeseen" action that picks Apple
Maps on iOS / macOS user-agents and Google Maps elsewhere
([`tiskilista-detail.component.ts`](../src/mVasu.Pwa/src/app/features/tiskilista/tiskilista-detail.component.ts)).
Navigating list → detail → back restores scroll position, search input,
filters and the loaded page via
[`LumoRouteReuseStrategy`](../src/mVasu.Pwa/src/app/core/routing/lumo-route-reuse-strategy.ts)
(opt-in per route through `data: { reuse: true }`).

## What the aihio deliberately does NOT do

- No domain-level views (customers, contracts, units) — those land in follow-up
  prompts.
- No SecurityStrategyComplex wiring — phase 5 lifts permission concerns once a
  domain view is added.
- No background sync, no offline data — basic PWA caching only.
- No i18n library — Finnish strings inlined; the language column on user
  settings stores intent for the time we add ngx-translate or `@angular/localize`.
- No production deployment pipeline — Key Vault wiring and App Service IaC are
  separate work.

## Cross-references

- Open decisions: [`design/open-decisions.md`](../design/open-decisions.md)
- Local development: [`local-development.md`](./local-development.md)
- Entra ID setup: [`entra-id-setup.md`](./entra-id-setup.md)
- Schema scripts: [`../db/scripts/README.md`](../db/scripts/README.md)
