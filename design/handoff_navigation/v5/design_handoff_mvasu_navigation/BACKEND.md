# BACKEND — Task Queue Contract & XAF/XPO Mapping

> ⚠️ **Lue ensin `EXISTING_ENTITIES.md`** — se sisältää oikeat XAF-kenttänimet ja relaatiot ruutukaappausten perusteella. Jos tämän dokumentin SQL-luonnokset ovat ristiriidassa EXISTING_ENTITIES:n kanssa, **EXISTING_ENTITIES voittaa**.
>
> **Tehtäväjono on suurin uusi backend-vaatimus.** Nykyinen XAF/XPO-malli ei tarjoa heterogeenistä tehtävälistaa natiivisti — eri entiteettityyppejä (Tutustumiskäynti, Tarjous, Irtisanominen, Valokuvaus…) on yhdistettävä yhdeksi virraksi tällä erikoispalvelulla. Tämä dokumentti määrittelee sopimuksen, jonka frontend olettaa.

---

## 1. Endpoint

```
GET  /api/tasks?from=2026-05-02&to=2026-05-09&user=me
```

### Query parametrit

| param | tyyppi | pakollinen | esimerkki | huom |
|---|---|---|---|---|
| `from` | ISO date | kyllä | `2026-05-02` | inclusive |
| `to`   | ISO date | kyllä | `2026-05-09` | inclusive |
| `user` | userId tai `me` | kyllä | `me` | rooli vaikuttaa filtteröintiin |
| `types` | string\[\] | ei | `visit,signature` | rajoita tehtävätyyppejä |
| `urgentOnly` | bool | ei | `true` | vain `urgent: true` rivit |

### Response

```ts
{
  groups: TaskGroup[]
  total: number
  generatedAt: string                    // ISO timestamp
}

type TaskGroup = {
  id: 'today' | 'tomorrow' | 'this-week' | 'later'
  label: string                          // "Tänään"
  date: string                           // "to 4.6."  (lokalisoitu Europe/Helsinki)
  tasks: Task[]
}

type Task = {
  id: string                             // unique across all task types
  type: TaskType
  accent: 'navy' | 'cta' | 'info' | 'warn'
  urgent: boolean
  when: {
    time: string                         // "09:00", "Heti", "Eilen"
    note?: string                        // "odottaa 2 pv"
    sortOrder: number                    // unix timestamp ms tai 0 = "Heti"
  }
  title: string                          // "Mannerheimintie 12 A 4"
  who?: string                           // "Maija Virtanen · 044 123 4567"
  meta?: string                          // "3 ilmoittautunutta · 2 vapaata"
  actions: TaskAction[]
  entityRef: EntityRef                   // takaisin alkuperäiseen XAF-entiteettiin
}

type TaskType =
  | 'visit-introduction'        // Tutustumiskäynti
  | 'visit-reservation'         // Varausesittely
  | 'open-house'                // Yleisesittely
  | 'signature-pending'         // Tarjous tai sopimus
  | 'inbox-termination'         // Saapunut irtisanomisilmoitus
  | 'inbox-signed'              // Palautunut sähköinen allekirjoitus
  | 'photo-scheduled'           // Valokuvaus tilattu
  | 'renovation-approval'       // Remontti vahvistettava
  | 'lead-callback'             // Liidi vaatii soittoa
  | 'desk-list-item'            // Tiskilistan rivi

type TaskAction = {
  label: string                          // "Avaa kohde"
  primary?: boolean
  kind: 'navigate' | 'phone' | 'sms' | 'mark-done' | 'cancel' | 'external'
  payload: any                           // kind-kohtainen
}

type EntityRef = {
  module: 'asma' | 'kire' | 'verkkokauppa' | 'core'
  entityType: string                     // XAF-luokka, esim. "Tutustumiskaynti"
  id: string                             // XAF-id
}
```

---

## 2. XAF/XPO-entiteettien yhdistäminen tehtäväjonoon

Tämä on keskeisin liiketoimintalogiikka, joka backendin tulee toteuttaa. Jokainen XAF-entiteetti, joka voi näkyä jonossa, **mapataan tehtävätyypiksi** alla olevien sääntöjen mukaan:

### 2.1. Tutustumiskäynti

```sql
SELECT … FROM Tutustumiskaynti
WHERE Aikataulu BETWEEN :from AND :to
  AND VastuuHenkilo = :user
  AND Tila IN ('sovittu', 'vahvistamatta')
```

→ `type: 'visit-introduction'`, `accent: 'navy'`
→ `urgent: true` jos < 2h `Aikataulu` ajasta

### 2.2. Varausesittely + Yleisesittely

Sama kuva kuin Tutustumiskäynti, mutta kentästä `EsittelyTyyppi`:

| EsittelyTyyppi | type |
|---|---|
| `Tutustumiskaynti` | `visit-introduction` |
| `Varausesittely` | `visit-reservation` |
| `Yleisesittely` | `open-house` |

### 2.3. Tarjous (allekirjoitusta odottaa)

```sql
SELECT … FROM Tarjous
WHERE Status = 'hyvaksytty_asiakkaalla'
  AND AllekirjoitusPaiva IS NULL
  AND Vastuuhenkilo = :user
```

→ `type: 'signature-pending'`, `accent: 'cta'`
→ `urgent: true` jos `LuontiPaiva < now - 2 days`
→ `when.time: 'Heti'`, `when.note: 'odottaa N pv'`

### 2.4. Sopimus (allekirjoitusta odottaa)

Sama `signature-pending` mutta lähde on Sopimus-entiteetti, jonka `Status = 'odottaa_allekirjoitusta'`.

### 2.5. Saapunut irtisanominen

```sql
SELECT … FROM Irtisanomisilmoitus
WHERE Tila = 'saapunut'
  AND KasittelyTila = 'avaamatta'
  AND VastuuHenkilo = :user
```

→ `type: 'inbox-termination'`, `accent: 'info'`
→ `when.time: 'Heti'`

### 2.6. Valokuvaus tilattu

```sql
SELECT … FROM Valokuvaus
WHERE Status = 'tilattu'
  AND Aikataulu BETWEEN :from AND :to
  AND TilaajaHenkilo = :user
```

→ `type: 'photo-scheduled'`, `accent: 'navy'`

### 2.7. Remontti vahvistettava

```sql
SELECT … FROM Remontti
WHERE Tila = 'odottaa_hyvaksyntaa'
  AND HyvaksyjaHenkilo = :user
```

→ `type: 'renovation-approval'`, `accent: 'warn'`

### 2.8. Liidi (soittoa odottaa)

```sql
SELECT … FROM Liidi
WHERE Status = 'avoin'
  AND ViimeinenKontakti < now - INTERVAL '2 days'
  AND VastuuHenkilo = :user
```

→ `type: 'lead-callback'`, `accent: 'navy'`

### 2.9. Tiskilistan rivit

`Tiskilista`-entiteetti, jonka `Status = 'avoin'` → `type: 'desk-list-item'`, `accent: 'navy'`.

---

## 3. Priorisointisäännöt (avoin päätös → ehdotus)

Jonossa kortit järjestetään **ryhmän sisällä** seuraavasti:

1. `urgent: true` ennen muita
2. Sitten `when.sortOrder` kasvavasti (aikajärjestys)
3. "Heti"-tyyppiset (`sortOrder = 0`) tulevat **ennen aikataulutettuja**, koska niiden viive on jo aikaa
4. Saman aikaleiman sisällä: `accent` järjestyksessä `cta → warn → info → navy`

> **OD-002 (open decision)**: pitäisikö "Heti"-tyyppiset olla aina päivän alussa vai sekoittua aikataulutettuihin? Ehdotus: alussa, kunnes käyttäjäpalaute kertoo muuta.

---

## 4. Suorituskyky

- Endpoint palauttaa **maksimissaan 50 tehtävää per ryhmä** → frontend näyttää "Näytä lisää"-rivin loppuun
- **Cache-Control**: `private, max-age=30, stale-while-revalidate=120`
- **ETag**-tuki pakollinen (kevyt 304-vaste yleisin tila)
- p95 latenssi tavoite: **< 400 ms** Helsinki-sijainnista
- Backend yhdistää eri entiteettitaulut **yhdellä CTE-kyselyllä** PostgreSQL/SQL Serverissä, ei N kierroksella

### Caching-strategia

Frontend:
```
1. Sivun avaus      → näytä localStorage cached jos < 60 s, samalla fetch background
2. Pull-to-refresh  → bypass cache, force fetch
3. Tab-switch back  → näytä cached, fetch jos > 30 s
```

---

## 5. Pikahaun endpoint

```
GET /api/search?q=manner&limit=5
```

### Response

```ts
{
  units: SearchHit[]
  people: SearchHit[]
  contracts: SearchHit[]
  actions: ActionHit[]               // staattinen lista, esim. "Tee uusi tarjous"
}

type SearchHit = {
  id: string
  title: string                       // raaka string, frontend tekee highlightin
  meta: string
  entityRef: EntityRef
}

type ActionHit = {
  id: string                          // 'new-offer', 'book-visit', …
  title: string
  meta: string
  navigate: string                    // sisäinen route
}
```

Suositus: Postgres `tsvector` + `pg_trgm` fuzzy match, tai DevExtreme-side-search jos sopii.

---

## 6. Toiminto-endpointit (TaskAction.kind)

| kind | endpoint | metodi | payload |
|---|---|---|---|
| `mark-done` | `/api/tasks/:id/complete` | POST | `{ note?: string }` |
| `cancel` | `/api/tasks/:id/cancel` | POST | `{ reason: string; note?: string }` |
| `phone` | (client-side) | — | `tel:+358441234567` |
| `sms` | (client-side) | — | `sms:+358441234567?body=...` |
| `navigate` | (client-side) | — | route string |
| `external` | (client-side) | — | URL, avataan uudessa tabissa |

`mark-done` ja `cancel` palauttavat **päivitetyn jonon ryhmän** (vain ne ryhmät joissa muutos), jotta frontend voi tehdä optimistisen päivityksen ilman koko jonon uudelleenhakua.

---

## 7. Käyttöoikeudet

- Endpoint suodattaa automaattisesti `VastuuHenkilo = currentUser`
- Esimies (rooli: `manager`) voi kysyä `?user=<id>` toisen henkilön jonon
- Pääkäyttäjä (rooli: `admin`) voi kysyä `?user=*` koko organisaation

XAF Security Layer hoitaa kentät joita käyttäjä ei saa nähdä — backend pitää lisätä testit ettei `Task.who` paljasta numeroita joiden katselu on rajoitettu.

---

## 8. Audit & lokitus

Jokainen `mark-done` / `cancel` kirjaa XAF Audit Trailiin:
- `taskId`, `taskType`, `entityRef`
- `userId`, `timestamp`
- `device: 'mvasu-pwa'`
- payload (note, reason)

---

## 9. Migraatio nykytilasta

Nykyisessä mVasussa ei ole "tehtäväjonoa" käsitteenä. Migraation askeleet:

1. **Vaihe 1**: Endpoint olemassa, mutta vain Tutustumiskäynti + Tarjous (kaksi suosituinta tyyppiä). Frontend näyttää nuo, "Näytä klassinen näkymä" -toggle aina näkyvissä. Tavoite: 2 viikkoa.
2. **Vaihe 2**: Lisää `inbox-termination`, `photo-scheduled`, `renovation-approval`. Käyttäjäkysely + telemetria.
3. **Vaihe 3**: Loput tehtävätyypit, "klassinen näkymä" piilotetaan oletuksena (Asetukset-välilehden alle).
4. **Vaihe 4**: Pikahaun integraatio entiteettien välillä. Vasta tässä vaiheessa "vasen valikko"-näkymä voidaan lopullisesti deprekoida.

---

## 10. Avoimet päätökset

| ID | Aihe | Status |
|---|---|---|
| OD-002 | "Heti"-tehtävien sijoitus jonossa | Ehdotus 4 §:ssä, vahvistus pyydetty |
| OD-003 | Esimiehen rooli — saako nähdä alaisten jonon? | Vaatii HR/lakipäätöksen |
| OD-004 | Tehtäväjonon muistutusilmoitukset (push) — minkä tyyppisistä, kuinka paljon ennen aikataulua? | Auki |
| OD-005 | Onko "klassinen XAF-näkymä" pakko säilyttää, vai voidaanko deprekoida? | Auki, riippuu vaiheen 1 telemetriastä |
