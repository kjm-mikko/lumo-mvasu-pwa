# Entra ID setup

Hand-runnable checklist for the **Lumo mVasu** App Registration in Kojamo's
Entra ID tenant. Aihio runs against a single registration that backs both the
PWA SPA and the Web API audience.

## Recorded values

| Field | Value |
| --- | --- |
| Display name | `Lumo mVasu` |
| Tenant ID | `bc727d7a-3368-4926-86f4-0972d3ac4637` (Kojamo Oyj) |
| Client ID | `d2f69daf-210b-4f64-b402-7914c4c9d0b4` |
| Application ID URI | `api://d2f69daf-210b-4f64-b402-7914c4c9d0b4` |
| Exposed scope | `access_as_user` |
| Redirect URI (dev) | `https://localhost:4200/auth/callback` |
| Sign-in audience | `AzureADMyOrg` (single tenant) |

These values are also in [`appsettings.json`](../src/mVasu.Api/appsettings.json),
[`environment.ts`](../src/mVasu.Pwa/src/environments/environment.ts), and the
[`README`](../README.md). Treat any one of them as the source of truth and keep
the others in sync.

## Step-by-step in Azure Portal

### 1. Create the App Registration

1. Sign in to [Azure Portal](https://portal.azure.com) with a Lumo / Kojamo
   account that has at least **Application Developer** or **Cloud Application
   Administrator** role.
2. Search for **Microsoft Entra ID** → **App registrations** → **+ New
   registration**.
3. Fill in:
   - **Name:** `Lumo mVasu`
   - **Supported account types:** *Accounts in this organizational directory only — Kojamo Oyj only (Single tenant)*
   - **Redirect URI:** select **Single-page application (SPA)**, value
     `https://localhost:4200/auth/callback`
4. Click **Register**.

### 2. Authentication

Open the new registration → **Authentication**.

- Add additional SPA redirect URIs as needed:
  - `https://localhost:4200` (root, for MSAL post-login redirect)
  - `https://localhost:4200/auth/callback` (already added in step 1)
- **Front-channel logout URL:** `https://localhost:4200/auth/login`
- **Implicit grant and hybrid flows:** leave both **Access tokens** and **ID
  tokens** unchecked. Auth Code + PKCE does not need them.
- **Allow public client flows:** Off.

### 3. Expose an API

Open **Expose an API** in the registration.

- **Application ID URI:** click **Set** → accept the proposed
  `api://d2f69daf-210b-4f64-b402-7914c4c9d0b4`.
- **Add a scope:**
  - Scope name: `access_as_user`
  - Who can consent: **Admins and users**
  - Admin consent display name: `Access Lumo mVasu API`
  - Admin consent description:
    *Allows the app to access the Lumo mVasu API on behalf of the signed-in user.*
  - User consent display name: `Käytä Lumo mVasu -palvelua`
  - User consent description: *Salli sovelluksen käyttää Lumo mVasua puolestasi.*
  - State: **Enabled**

### 4. API permissions

Open **API permissions**.

Add the following delegated permissions:

- **Microsoft Graph**:
  - `openid`
  - `profile`
  - `email`
  - `offline_access`
  - `User.Read`
- **My APIs → Lumo mVasu**:
  - `access_as_user`

Click **Grant admin consent for Kojamo Oyj** to skip the per-user consent
dialog at first sign-in.

### 5. Token configuration (optional claims)

Open **Token configuration** → **+ Add optional claim**.

- **ID token:** `email`, `preferred_username`, `upn`, `family_name`, `given_name`
- **Access token:** `email`, `preferred_username`, `upn`

The `EmailResolver` reads `Identity.Name` (which Microsoft.Identity.Web maps
from `preferred_username` by default) and falls back to `email` / `upn` claims,
so all three should be present on the access token.

### 6. Manifest tweaks

Open **Manifest** and confirm the following entries (the portal sometimes
defaults `accessTokenAcceptedVersion` to `null`, which means v1 — we want v2):

```json
{
  "accessTokenAcceptedVersion": 2,
  "signInAudience": "AzureADMyOrg"
}
```

### 7. No client secret

A SPA + PKCE flow does not use a client secret. **Do not** add one under
**Certificates & secrets** for the PWA — the secret would be impossible to
keep, well, secret.

## Verifying the registration

After saving everything, run through the auth flow once:

1. Start the API: `dotnet watch run --project src/mVasu.Api`.
2. Start the PWA: `npm start --prefix src/mVasu.Pwa`.
3. Open `https://localhost:4200/home` → MSAL redirects you to Entra.
4. Sign in with a Lumo account that satisfies the resolver criteria
   (`mVasuEnabled`, `IsActive`, `vvoad` UserName prefix or
   `@kojamo.onmicrosoft.com` email).
5. After redirect: `/home` should render with your display name in the header
   and a `GET /api/me` should appear in the API logs returning a real
   `xVasuSecuritySystemUser.Oid`.

If the sign-in completes but `/api/me` returns 403, the resolver did not find
a matching user — verify against the database that `mVasuEnabled = 1` and the
email/UserName match the rules in
[`Authentication/EmailResolver.cs`](../src/mVasu.Api/Authentication/EmailResolver.cs).

## Per-environment registrations

The aihio uses a single registration for dev. Production should add its own
redirect URIs (and ideally its own registration so MFA and Conditional Access
policies can differ). When that lands, update
[`environment.prod.ts`](../src/mVasu.Pwa/src/environments/environment.prod.ts)
with the production client ID + redirect URIs and create a new SQL script
under `db/scripts/` for any environment-specific seed data.
