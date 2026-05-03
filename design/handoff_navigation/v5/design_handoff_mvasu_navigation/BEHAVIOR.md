# BEHAVIOR — Interactions, Animations, State

> Tämä dokumentti kuvaa, **miten** näkymät reagoivat käyttäjän toimintaan, mitä animaatioita kannattaa toteuttaa ja millaista tilaa kukin näkymä tarvitsee. Visuaaliset spesifikaatiot ovat erikseen `SCREENS.md`:ssä.

---

## 1. Globaalit interaktioperiaatteet

### Touch targets
- **Minimi 44×44 px** kaikille klikattaville elementeille (`--lumo-touch-target`)
- Listariveille minimi 48 px korkeus, mielellään 56–64

### Tap feedback
- iOS-tyyliä **ei** käytetä — sen sijaan Material 3:n `state-layer`:
  - tap-down: `currentColor` 12 % alpha overlay
  - hover (touch-laitteilla ei näkyvissä, desktop PWA:ssa toimii): 8 % alpha
  - duration: 80 ms ease-out (`--lumo-duration-fast`)

### Vibrations / haptics
- **Lyhyt haptic** (`navigator.vibrate(8)`) ensisijaisten CTA-toimintojen (esim. "Merkitse pidetyksi") onnistumisesta
- Ei tap-haptiikkaa muuten — Lumo-brändi on rauhallinen, ei "pelillistetty"

### Pull-to-refresh
- Kaikki listanäkymät tukevat (Tehtävät, Asukkaat, Kohteet, Sopimukset)
- Spinner värinä `--lumo-primary` (#002663)
- Kynnys 60 px

### Loading patterns
- **Skeleton-rivit** ensisijainen (3–5 placeholderia, shimmer 320 ms loop)
- **Spinner** vain aktioissa (esim. CTA-painike), ei näkymäkohtaisesti
- Ei "Loading…" -tekstiä missään — visuaalinen on aina riittävä

### Network errors
- Inline error banner sivun yläosassa, 1 px solid `#a32d2d` border, bg `#fcebeb`, retry-button
- Ei full-screen-virhesivuja paitsi authentikaatiovirheissä

---

## 2. Tehtävät-näkymä (01)

### Lifecycle

```
[init]
  → fetch tasks (today + tomorrow + this week)
  → if cache: render cached, fetch in background
  → on fetch success: diff & animate added/removed cards

[card tap]
  → navigate /tasks/:id (push transition)
  → save scroll position so back-nav restores

[action button tap]
  → run action immediately (no detail view)
  → optimistic UI: card greys out 200 ms
  → on success: card removes itself with collapse-animation 320 ms
  → on failure: revert + toast at bottom

[pull-to-refresh]
  → re-fetch
  → if new urgent task arrives: scroll to top automatically + 1 brief pulse

[every 60 s]
  → silent background re-fetch (battery-aware: skip if backgrounded)
```

### Animations
- **Card enter**: opacity 0 → 1, translateY 8px → 0, 200 ms `--lumo-easing`
- **Card remove**: height collapse + opacity → 0, 320 ms
- **Day-header sticky**: kun käyttäjä vierittää alaspäin, päiväotsikko jää kiinni topbarin alle (`position: sticky; top: 64px`)

### Tila

```ts
type TasksState = {
  groups: Array<{
    id: 'today' | 'tomorrow' | 'this-week' | 'later'
    label: string         // "Tänään"
    date: string          // "to 4.6."
    tasks: TaskCard[]
  }>
  loading: boolean
  refreshing: boolean
  error: ApiError | null
  lastFetched: Date | null
}

type TaskCard = {
  id: string
  type: 'visit' | 'open-house' | 'signature' | 'inbox' | 'photo' | 'renovation' | …
  accent: 'navy' | 'cta' | 'info' | 'warn'
  urgent: boolean
  when: { time: string; note?: string }      // server-rendered for locale
  title: string
  who?: string
  meta?: string
  actions: TaskAction[]
  // back-references for deep-link
  entityRef: { module: string; id: string }
}

type TaskAction = {
  label: string
  primary?: boolean
  kind: 'navigate' | 'phone' | 'sms' | 'mark-done' | 'cancel' | …
  payload?: any
}
```

### Edge cases
- **Tyhjä päivä**: jos `groups[].tasks.length === 0`, näytetään 1 illustration + "Päivän tehtävät on hoidettu. 👏"
- **>50 tehtävää tänään**: päivä-ryhmä virtualisoituu (Angular CDK `cdkVirtualFor`)
- **Aikavyöhyke**: kaikki ajat palvelimen aikavyöhykkeessä (Europe/Helsinki); client näyttää sellaisenaan

---

## 3. Asukkaat-näkymä (02)

### Lifecycle

```
[init]
  → load 50 first results (alphabetical), virtualize rest
  → cache to IndexedDB for offline browsing

[search input]
  → debounce 200 ms
  → server-side fuzzy match: name, address, phone, email
  → highlight matched substring with <mark>

[filter button]
  → opens bottom sheet
  → filters: Asukkaat / Hakijat / Entiset, sopimustyyppi, kaupunki
  → applied filters → chip row above list

[row tap]
  → navigate /people/:id
```

### Tila

```ts
type PeopleState = {
  query: string
  filters: {
    role?: 'tenant' | 'applicant' | 'former'
    contractType?: string
    city?: string
  }
  results: Person[]
  total: number
  hasMore: boolean
  loading: boolean
}

type Person = {
  id: string
  name: string                  // "Sukunimi, Etunimi"
  initials: string              // 2-kirjaiminen avatar
  meta: string                  // "Asukas · M-katu 8 B 12"
  tag?: { label: string; color: 'default' | 'cta' | 'info' }
  phone?: string
  email?: string
}
```

### Edge cases
- **Diakriitit**: Ä/Ö indeksoidaan oikein suomenkielisessä järjestyksessä (lopussa, ei ennen A:ta)
- **Recent contacts**: tyhjällä haulla näytetään ensin 5 viimeksi avattua

---

## 4. Lisää-näkymä (03)

### Lifecycle

```
[init]
  → load profile (cached aggressively, refresh 1×/päivä)
  → load badge counts for "Aineisto"-osion rivit (kevyt API-kutsu)

[row "Teema"]
  → opens bottom sheet:
       • Vaalea
       • Tumma
       • Käytä järjestelmäasetusta
  → applies immediately, persist to localStorage

[row "Kirjaudu ulos"]
  → confirmation modal: "Haluatko varmasti kirjautua ulos?"
  → on confirm: clear tokens, redirect /login
```

### Tila

```ts
type MoreState = {
  user: { name: string; role: string; office: string; initials: string }
  badges: {
    offers: number
    vacant: number
    renovations?: number
  }
  preferences: {
    theme: 'light' | 'dark' | 'system'
    language: 'fi' | 'en'      // myöhemmin
    location: 'allowed' | 'blocked' | 'unset'
    notifications: string[]
  }
}
```

---

## 5. Tehtävän tarkka näkymä (04)

### Lifecycle

```
[init]
  → fetch task by id (incl. customer, history, related entities)
  → if linked entity (esim. Asunto) cached in client → render synchronously

[customer phone-icon tap]
  → tel:+358441234567 (suora soitto)

[row "Avaa kohde Lumo Verkossa"]
  → external link to public listing (open in browser, not in PWA)

[row "Tee tarjous tästä"]
  → navigate /offers/new?prefill=<task.id>

[row "Merkitse pidetyksi"]
  → confirmation modal "Käynti pidetty?" + optional note field
  → on confirm: PUT /tasks/:id { status: 'done' }
  → on success: pop back to Tehtävät, card collapse-animates out

[row "Peruuta käynti"]
  → reason-picker bottom sheet (predefined reasons + "Muu")
  → on confirm: PUT /tasks/:id { status: 'cancelled', reason }

[note-block tap]
  → expand to textarea
  → on blur: PUT /notes/:id

[footer CTA "Avaa kohde"]
  → navigate /units/:unitId
```

### Animations
- **Hero block**: kun näkymä avautuu, hero-time pill ja title fade-in 200 ms peräkkäin (40 ms staggered)
- **Note expand**: max-height 0 → 200px transition 200 ms

### Tila

```ts
type TaskDetailState = {
  task: TaskDetail | null
  loading: boolean
  saving: 'idle' | 'note' | 'action'
  error?: ApiError
}

type TaskDetail = {
  id: string
  type: TaskType
  when: { time: string; note?: string; sortOrder: number }
  title: string
  subtitle: string                  // "2 h, 47 m² · vapaa 1.7."
  customer?: {
    id: string
    name: string
    initials: string
    phone?: string
    email?: string
  }
  actions: TaskAction[]
  notes: { id: string; body: string; updatedAt: string } | null
  primaryCta: { label: string; navigate: string }
}
```

---

## 6. Pikahaku (05)

### Lifecycle

```
[open]
  → autofocus input
  → if previous query in session: prefill

[typing]
  → debounce 200 ms
  → min length 2
  → cancel in-flight request on new keystroke (AbortController)

[empty input]
  → show "Viimeksi etsitty" -section: 5 entries from sessionStorage

[arrow up/down]
  → navigate rows; first row pre-selected on result render

[Enter]
  → trigger selected row's action

[Escape / "Peru"]
  → close overlay; pop back

[row tap]
  → for entities: navigate to entity detail
  → for actions: trigger action (may navigate or open modal)
```

### Animations
- **Open**: overlay slide up from bottom, 240 ms `--lumo-easing`
- **Result groups**: stagger 30 ms per group on initial render
- **Highlight `<mark>`**: pure CSS, no animation

### Tila

```ts
type QuickSearchState = {
  query: string
  results: {
    units: Unit[]
    people: Person[]
    contracts: Contract[]
    actions: Action[]
  }
  selectedRowId: string | null
  loading: boolean
  recentQueries: string[]      // sessionStorage
}
```

### Suorituskyky
- API tukee `?limit=5` per ryhmä → max 20 riviä kerralla
- Tulokset cachetetaan queryn mukaan 60 s

---

## 7. Navigation transitions (kaikki näkymät)

| Mistä | Mihin | Transition | Duration |
|---|---|---|---|
| Tab | Tab (bottom-nav) | crossfade | 120 ms |
| List | Detail | iOS-tyylinen slide-from-right | 280 ms |
| Detail | List (back) | slide-to-right | 280 ms |
| Any | Modal | fade + scale 0.96 → 1 | 200 ms |
| Any | Bottom-sheet | slide up | 280 ms |
| Search | Search-overlay | slide up + fade | 240 ms |

Käytä Angular Animations -modulia tai natiivista `view-transition-name` -CSS:ää (joka toimii Chromessa ≥111).

---

## 8. Offline-käyttäytyminen

mVasu on PWA:n omainen, ja seuraavien näkymien on toimittava offlinena (cached data):
- **Tehtävät** — viimeksi haetun jonon lukunäkymä, ei toimintoja (toiminnot näyttävät "Yhteyttä ei ole" -toastin)
- **Asukkaat** — viimeisen synkronoinnin lista, haku rajoitettu paikalliseen
- **Lisää** — täysin offline

Online-tilan palautuessa banner yläreunassa: "Yhteys palautui — synkronoidaan..." 2 s viiveellä häviää.

---

## 9. Saavutettavuus

- **Kontrasti**: WCAG AA, kaikki body-tekstit ≥4.5:1 (token-arvot ovat valmiiksi laskettuja)
- **Focus-rings**: 3 px outline `var(--lumo-focus-ring)` näkyvissä kaikkien klikattavien yli (näppäimistöllä, ei pelkällä hiirellä — käytä `:focus-visible`)
- **Aria**:
  - Bottom tabs: `role="tablist"`, kukin tab `role="tab"`, `aria-selected`
  - TaskCard: koko kortti on `<button>` tai `<a>` jos koko alue on klikattava
  - Day-header: `<h2>` ja muut otsikot semanttiset
- **Reduced motion**: `@media (prefers-reduced-motion: reduce)` → kaikki transitionit `0.01ms`
- **Screen reader -tarkistuslista** suomeksi: VoiceOver iOS / TalkBack Android

---

## 10. Telemetria (suositus)

Mittaa:
- Tehtävät-välilehden time-to-first-task (rendaus + ensimmäinen kortti näkyvissä)
- Pikahaun query-latency p50/p95
- TaskCard tap → detail open -aika
- Käyttäjäkohtaisia: keskimäärin tehtäviä / päivä, jonon valmistumisprosentti, suosituin entry-point
