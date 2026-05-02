# Lumo mVasu PWA

Seuraavan sukupolven Lumo mVasu — DevExtreme Angular PWA + ASP.NET Core 10 Web API.

> **Tila:** Aihio (skeleton). Vaihe 9/14 — Entra ID -kirjautuminen kytketty PWA:han (MSAL Authorization Code + PKCE).

---

## Pikaopas

```powershell
# Restore + build
dotnet restore Lumo.mVasu.slnx
dotnet build Lumo.mVasu.slnx

# Aja API (kehityksessä)
dotnet watch run --project src/mVasu.Api

# Aja testit
dotnet test Lumo.mVasu.slnx
```

API käynnistyy oletuksena `https://localhost:5001` (varsinainen portti vahvistetaan vaiheessa 2). Scalar UI tulee saataville `/scalar/v1`-polkuun vaiheessa 2.

PWA-puoli (`src/mVasu.Pwa/`) lisätään vaiheessa 8.

---

## Edellytykset

| Työkalu | Versio | Käyttö |
| --- | --- | --- |
| .NET SDK | 10.0.103 (lukittu `global.json`-tiedostolla) | Backend |
| Node.js | ≥ 22 LTS | PWA build (vaiheesta 8 alkaen) |
| npm | ≥ 11 | PWA-paketit |
| SQL Server | Olemassa oleva instanssi (käyttäjäkohtainen) | XPO-data, vaiheesta 4 alkaen |
| Visual Studio Code | Uusin | IDE (suositus) — kts. `.vscode/extensions.json` |

### Ensikäyttö VS Codessa

1. Avaa repo VS Codessa → hyväksy "Recommended extensions" -kehotus
2. F1 → "Tasks: Run Build Task" → `build:solution`
3. F5 → "Launch API" (vaiheesta 2 alkaen tämä avaa Scalar UI:n automaattisesti)

### HTTPS dev-sertin asentaminen (kerran per kone)

Sekä `mVasu.Api` että `mVasu.Pwa` ajavat HTTPS:llä dev-ympäristössä. Aseta luotettu dev-sertti kerran:

```powershell
dotnet dev-certs https --trust
```

Tämä luo (tai varmistaa) sertin ja merkitsee sen luotetuksi Windowsin sertifikaattivarastoon. Sama sertti kelpaa sekä Kestrelille (`https://localhost:7216`) että muille `localhost`-palvelimille.

PWA käynnistyy `https://localhost:4200`:llä — Angular Dev Server (`ng serve --ssl`) generoi oman self-signed-sertin jos sille ei anneta avain-/varmenne-tiedostoa, ja selain pyytää hyväksymisen kerran. Halutessa voit vaihtaa Angular-serverin käyttämään dotnet-dev-cert:iä:

```powershell
# Export dotnet dev cert as a .pem pair Angular can consume
dotnet dev-certs https --export-path "$env:USERPROFILE\.aspnet\https\localhost.pem" --format Pem --no-password
```

ja päivittämään [src/mVasu.Pwa/package.json](src/mVasu.Pwa/package.json):n start-scriptiin:

```text
ng serve --ssl --ssl-key=<polku>/localhost.key --ssl-cert=<polku>/localhost.pem --host=localhost --port=4200
```

Aihiossa default `ng serve --ssl` riittää — selain hyväksyy self-signed-sertin kerran ja PWA toimii tämän jälkeen normaalisti.

---

## Solution-rakenne

```text
lumo-mvasu-pwa/
├── .vscode/                          VS Code -konfiguraatio (debug, tasks, suositukset)
├── design/                           Suunnitteluvaiheen tuotokset (tokenit, overrides, päätökset)
├── docs/                             Architecture, local development, Entra setup
├── src/
│   ├── mVasu.Api/                    ASP.NET Core 10 Web API (minimal API, Scalar OpenAPI)
│   ├── mVasu.Api.Contracts/          Jaetut DTO-luokat (TS-generointi backendin yli)
│   └── mVasu.Pwa/                    Angular 19 + DevExtreme 25.2 + MSAL (vaihe 8+)
├── tests/
│   ├── mVasu.Api.Tests/              xUnit + WebApplicationFactory
│   └── mVasu.Pwa.Tests/              Karma + Jasmine (vaihe 8+)
├── .editorconfig
├── .gitignore                        .NET + Node yhdistetty
├── global.json                       Lukitsee .NET SDK -version
├── Lumo.mVasu.slnx                   Solution (XML-pohjainen .slnx, .NET 10)
├── nuget.config                      Lisää Lumon Azure DevOps Artifacts -feedin xVasu/Vasu-paketeille
└── README.md
```

---

## NuGet-feedin autentikointi (Azure DevOps Artifacts)

`xVasu.Module` ja muut `Vasu.*`-paketit ladataan privaatista feedistä `kjm-sk-ado` (Azure DevOps). Kehittäjän koneella tarvitaan **kerran** Azure Artifacts Credential Provider, joka hoitaa autentikoinnin automaattisesti:

```powershell
# Asenna credential provider (vaaditaan kerran per kone)
iex "& { $(irm https://aka.ms/install-artifacts-credprovider.ps1) }"
```

Ensimmäisellä `dotnet restore`-ajolla avautuu Microsoft-kirjautuminen → kirjaudu työsähköpostilla. Token cachetetaan paikallisesti.

Vaihtoehto: PAT (Personal Access Token) Azure DevOpsista, jolla `Packaging (read)`-scope, ja se sijoitetaan `~/.nuget/NuGet/NuGet.Config`-tiedostoon `<packageSourceCredentials>`-osion alle. Älä committaa PAT:ia repoon.

> **Huom:** ennen kuin credential provider on asennettu, näkyy varoitus
> `NU1900: Error occurred while getting package vulnerability data… kjm-sk-ado/...`.
> Build menee silti läpi, kunhan repo ei vielä viittaa privaatti-feedin paketteihin (vaihe 1). Vaiheessa 4 (XPO + xVasu) autentikointi muuttuu pakolliseksi.

---

## Tietokantayhteys

Connection stringiä **ei pidetä repossa**. Aseta paikallisesti User Secretsiin:

```powershell
cd src/mVasu.Api
dotnet user-secrets init
dotnet user-secrets set "ConnectionStrings:VasuDb" "<connection string omasta turvallisesta lähteestä>"
```

Tuotannossa connection string luetaan Azure Key Vaultista (pipeline-konfiguraatio rakennetaan myöhemmin).

### Yhteyden testaaminen

API tarjoaa anonyymin endpointin yhteyden todentamiseen:

```http
GET https://localhost:7216/api/health/db
```

Vastaa `200 OK` + `{ connected: true, durationMs: ..., error: null }` kun XPO avaa session ja saa `SELECT 1` -tuloksen onnistuneesti, muuten `503 Service Unavailable` ja `error`-kenttä kertoo poikkeuksen tyypin (esim. `SqlException`).

XPO-rekisteröinti rakennettu [src/mVasu.Api/Data/XpoServiceCollectionExtensions.cs](src/mVasu.Api/Data/XpoServiceCollectionExtensions.cs):

- `IXpoDataStoreProvider` singleton — connection string luetaan vasta ensimmäisen request:in yhteydessä
- `IObjectSpaceProvider` singleton, threadSafe, jaettu data layer kaikkien requestien välillä
- `xVasu.Data.Security.xVasuSecuritySystemUser`, `xVasuSecuritySystemRole` ja `MVasuUserSettings` rekisteröidään `XafTypesInfo`:hen ennen ensimmäistä `IObjectSpace.FindObject`-kutsua
- `AutoCreateOption.SchemaAlreadyExists` — sovellus **ei koskaan** muokkaa kantarakennetta

### Schema-muutokset (manuaalisesti)

Kaikki DDL-muutokset (uudet taulut, sarakkeet) toimitetaan käsin ajettavina SQL-skripteinä [`db/scripts/`](db/scripts/)-kansiossa. Sovelluksen ajaminen ei luo eikä muokkaa tauluja missään ympäristössä.

Vaiheen 5 schema-muutos: [`db/scripts/001_create_mvasuusersettings.sql`](db/scripts/001_create_mvasuusersettings.sql) — luo `MVasuUserSettings`-taulun. Aja kerran ennen ensimmäistä `/api/me`-kutsua testikannassa.

---

## Entra ID -konfiguraatio

| Asia | Arvo |
| --- | --- |
| Tenant ID | `bc727d7a-3368-4926-86f4-0972d3ac4637` (Kojamo Oyj, single tenant) |
| App Registration | `Lumo mVasu` — yhdistetty PWA SPA + Web API |
| Client ID | `d2f69daf-210b-4f64-b402-7914c4c9d0b4` |
| Scope | `api://d2f69daf-210b-4f64-b402-7914c4c9d0b4/access_as_user` |

App Registrationin tarkat Azure Portal -asetukset dokumentoidaan `docs/entra-id-setup.md`-tiedostoon vaiheessa 3.

### Suojattujen endpointtien testaaminen kehityksessä

`/api/me` vaatii bearer-tokenin Lumo Entra ID -tenantilta scopella `access_as_user`. Tokenin hankinta devissä:

```powershell
# Vaihtoehto 1 — Azure CLI (vaatii Lumo-tilin)
az login
az account get-access-token --resource api://d2f69daf-210b-4f64-b402-7914c4c9d0b4 --query accessToken -o tsv
```

Vaihe 9:n jälkeen PWA hoitaa tokenin handlaamisen automaattisesti `https://localhost:4200`-osoitteessa.

[`src/mVasu.Api/mVasu.Api.http`](src/mVasu.Api/mVasu.Api.http) sisältää valmiit REST Client -kutsut testaukseen.

---

## Branch-strategia

- **`main`** — vain releaset
- **`develop`** — integraatiokehitys
- **`feature/*`** — yksittäiset työt, mergetään `develop`:iin

Conventional Commits: `feat(api): ...`, `fix(pwa): ...`, `chore(deps): ...`.

---

## Toteutusjärjestys (vaiheet)

1. ✅ Solution-rakenne + projektit + .vscode/ + global.json + .gitignore + README — *valmis*
2. ✅ Backend: minimi-API käynnistyy, `/api/health`, Scalar UI — *valmis*
3. ✅ Backend: Entra ID -auth (kovakoodattu testidata, `/api/me` toimii) — *valmis*
4. ✅ Backend: XPO + xVasu-moduuli, `/api/health/db` — *valmis*
5. ✅ Backend: email-pohjainen käyttäjä-resolver, MVasuUserSettings, `/api/me` palauttaa XPO-tietoja — *valmis*
6. ✅ Backend: `PUT /api/me/settings` päivittää käyttäjäkohtaiset asetukset — *valmis*
7. ✅ Backend: `PUT /api/me/location-consent`, `POST /api/me/location` (sijaintipalvelut) — *valmis*
8. ✅ Frontend: Angular 19 + DevExtreme 25.2 + PWA + Lumo-tokenit (runko + reitit + ThemeService) — *valmis*
9. ✅ Frontend: MSAL login flow (Authorization Code + PKCE, sessionStorage, MsalGuard, MsalInterceptor) — *valmis*
10. Frontend: HTTP-interceptor + `/api/me`-kutsu
11. Frontend: Etusivu (greeting, kortit, bottom tab / sidebar)
12. Frontend: Asetukset-sivu
13. Frontend: LocationService + sijaintipalvelut-osio
14. README + docs päivitetty kokonaisuudessaan

---

## Kirjoitusasut

Lumon brändiohjeissa pakolliset:

- **Lumo** kirjoitetaan AINA isolla alkukirjaimella
- **mVasu** kirjoitetaan AINA tarkalleen näin: pieni `m`, iso `V`, loput pieniä

Säännöt koskevat kaikkea näkyvää tekstiä: UI-stringit, README, koodikommentit, commit-viestit, dokumentaatio. Tiedostonimien all-lowercase-konventiot (NPM-paketit, Git-repon `lumo-mvasu-pwa`-nimi) ovat poikkeus.

Yksityiskohtaiset suunnittelupäätökset: [`design/open-decisions.md`](design/open-decisions.md).

---

## Tietoturva

- ❌ Ei oikeita asiakas- tai henkilötietoja repossa eikä commit-viesteissä — käytä esimerkkejä `Käyttäjä_001`, `demo@lumokodit.fi`
- ❌ Ei salaisuuksia repossa — connection stringit, client secretit, API-avaimet User Secretsiin (dev) tai Key Vaultiin (tuotanto)
- ✅ HTTPS aina, myös devissä — `dotnet dev-certs https --trust` ennen ensimmäistä ajoa
- ✅ Loggauksessa ei tokeneja, salasanoja, eikä PII:tä request bodyissa

---

© Lumo Kodit IT
