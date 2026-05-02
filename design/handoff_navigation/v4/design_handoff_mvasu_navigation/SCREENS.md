# SCREENS — Per-screen Specification

> Jokainen näkymä on dokumentoitu niin tarkasti, että toteutus voi tapahtua ilman lisäkysymyksiä. Hex-arvot, font-koot ja px-marginaalit ovat **otettavissa sellaisenaan**. Käytä kuitenkin `tokens.css`:n CSS-muuttujia — älä kopioi hex-arvoja koodiin.
>
> ⚠️ **Komponenttivalinnat**: tässä dokumentissa kuvatut HTML-rakenteet ovat **vain visuaalinen referenssi**. Toteutus käyttää **DevExtreme Angular** -komponentteja (`dx-list`, `dx-data-grid`, `dx-popup`, `dx-form`, `dx-button`, `dx-tabs`, `dx-tree-view`, `dx-toast`, …). Komponenttimappaus per näkymä: ks. **`DEVEXTREME.md`**.

---

## Yhteiset rakennuspalikat

### Container (kaikki näkymät)

```
.app
  width: 100%
  height: 100% (laitekehyksessä)
  display: flex; flex-direction: column
  background: var(--lumo-bg)            // #ffffff
  color: var(--lumo-text-primary)       // #0f172a
  font-family: var(--lumo-font-body)
```

### Topbar — yläpalkki

```
height:           min 64px (sisältää eyebrowin)
padding:          14px 16px 10px
border-bottom:    1px solid #f1f5f9
display:          flex; align-items: center; gap: 8px

  .eyebrow         font: 400 12px Inter; color: #64748b
  .title           font: 600 22px Playfair Display; color: #0f172a; line-height: 1.1
  .icon-btn        40×40, border-radius: full, color: #475569
                   hover bg: #f1f5f9
```

### Bottom tabs (A:n osa)

```
height:           68px
border-top:       1px solid #e2e8f0
background:       #ffffff
display:          grid; grid-template-columns: repeat(5, 1fr)

  .tab             flex column, gap: 3px
                   font: 500 10.5px Inter
                   inactive color: #94a3b8
                   active color:   #002663
  .tab.active::before
                   3px high, 32px wide, #002663 indicator
                   sijaitsee tabbarin yläreunassa keskitettynä
```

Ikonit käytä `lucide-react` -tasoisia ohuita stroke-ikoneita (stroke-width 2). Pakkauksen `screens.jsx` sisältää inline-SVG-versiot — käytä noita visuaalireferenssinä mutta vaihda Angular-projektin omaan ikoniratkaisuun.

Välilehtien järjestys (vasen → oikea):

| # | id | label | ikoni |
|---|---|---|---|
| 1 | `tasks` | Tehtävät | inbox |
| 2 | `people` | Asukkaat | users |
| 3 | `units` | Kohteet | building |
| 4 | `contracts` | Sopimukset | file |
| 5 | `more` | Lisää | three-dots |

---

## 01 · Tehtävät (etusivu, A+C:n C-puoli)

**Tarkoitus**: Näyttää käyttäjälle päivän työ aikajärjestyksessä, ryhmiteltynä päiväotsikoiden alle. Korvaa "etusivun moduulivalikon".

### Layout

```
┌─ Topbar ────────────────────────────────┐
│ Hyvää huomenta, Anna                    │
│ Tehtävät                       🔍 🔔   │
├─────────────────────────────────────────┤
│ Tänään · to 4.6.            5 TEHTÄVÄÄ  │   ← day-header
│                                          │
│ ┌─ TaskCard.urgent ───────────────────┐ │
│ │ 09:00   📅 TUTUSTUMISKÄYNTI         │ │
│ │         Mannerheimintie 12 A 4      │ │
│ │         Maija Virtanen · 044…       │ │
│ │         [Avaa kohde] [Soita]        │ │
│ └─────────────────────────────────────┘ │
│ ┌─ TaskCard ──────────────────────────┐ │
│ │ 11:30   📅 YLEISESITTELY            │ │
│ │ ...                                  │ │
│ └─────────────────────────────────────┘ │
│                                          │
│ Huomenna · pe 5.6.          3 TEHTÄVÄÄ  │
│ ...                                      │
├─────────────────────────────────────────┤
│   Tehtävät | Asukkaat | Kohteet | …     │   ← bottom tabs
└─────────────────────────────────────────┘
```

### Topbar-erikoisuus

- **Eyebrow**: dynaaminen tervehdys — "Hyvää huomenta", "Hyvää päivää", "Hyvää iltaa", + käyttäjän etunimi
- **Title**: "Tehtävät"
- **Trailing**: search-icon (avaa Quick Search), bell-icon (ilmoitukset, badge jos uusia)

### Day-header

```
margin:           8px 0 12px (paitsi ensimmäinen, jossa 0 0 12px)
display:          flex; align-items: baseline; gap: 8px

  .day             font: 600 14px Playfair Display; color: #002663
  .date            font: 400 13px Inter; color: #64748b
  .count (margin-left: auto)
                   font: 500 11px Inter; color: #94a3b8
                   text-transform: uppercase; letter-spacing: 0.06em
```

Päiväotsikkojen järjestys: **"Tänään"** → **"Huomenna"** → **"Tällä viikolla"** (jos 4 päivän sisällä) → **"Myöhemmin"**.

### TaskCard

```
display:          grid; grid-template-columns: 64px 1fr; gap: 12px
padding:          14px (vasen 11px kun border-left aksenttina)
background:       #ffffff (urgent: #fef9fc)
border:           1px solid #e2e8f0
border-left:      3px solid <accent>
border-radius:    12px
box-shadow:       0 1px 2px rgba(15,23,42,0.04)
margin-bottom:    10px
```

#### Aksenttivärit (kortin vasen reuna + ensisijainen toimintonappi)

| Tehtävätyyppi | accent | hex | milloin |
|---|---|---|---|
| Esittelyt, default | `navy` | `#002663` | tutustumiskäynti, yleisesittely, varausesittely, valokuvaus |
| Allekirjoitusta odottaa | `cta` | `#ca2774` | tarjous valmis, sopimus odottaa |
| Saapunut-tyyppi | `info` | `#1c808f` | irtisanomisilmoitus, sähköinen allekirjoitus palautunut |
| Vaatii hyväksynnän | `warn` | `#ba7517` | remontti, kustannusarvio |

#### Kortin osat

```
.when (vasen kolumni, 64px)
  .time            font: 600 15px Inter; color: #0f172a
                   ("09:00", "Heti", "Eilen")
  .note            font: 400 11px Inter; color: #94a3b8
                   ("odottaa 2 pv", optional)

.body
  .type-row        flex, align-items: center, gap: 6px
    icon           14px stroke-2, color: #475569
    label          font: 500 11px Inter; uppercase; letter-spacing: 0.06em; color: #475569
    .urgent-pill   bg: #fdedf4; color: #ca2774; font: 500 10px;
                   padding: 2px 7px; border-radius: full
                   prefix: flame-icon 12px

  .title           font: 500 15px Inter; color: #0f172a; line-height: 1.3
  .who             font: 400 13px Inter; color: #475569
  .meta            font: 400 12px Inter; color: #94a3b8

  .actions (margin-top: 8px)
    flex; gap: 8px; flex-wrap: wrap
    .a-btn         min-height: 32px; padding: 6px 12px; border-radius: 6px
                   default: bg transparent, border 1px solid #cbd5e1, color #002663
                   primary: bg <accent>, color #fff
```

### Tilat

- **Tyhjä päivä** ("Ei tehtäviä tänään"): keskitetty placeholder, ei kortteja, friendly-illustration kokoa 80×80 (esim. valmiina rasti tai kahvikuppi). Teksti: "Päivän tehtävät on hoidettu. 👏"
- **Latausvaihe**: 3 skeleton-korttia, shimmer-efekti `--lumo-duration-slow`
- **Virhe**: kortti yläosaan, punainen border, "Tehtäviä ei voitu ladata" + retry-button

### Käyttäytyminen

- Korttin tap (mihin tahansa kohtaan paitsi nappiin) → navigaatio tehtävän tarkkaan näkymään (`/tasks/:id`)
- Toimintonappien tap → suoraan toiminto ilman tarkkaa näkymää (esim. "Soita" → `tel:` linkki)
- Pull-to-refresh tukee uudelleenlatausta
- Päiväotsikoita ei voi sulkea (sticky-tehosteen tarkennetaan myöhemmin)

---

## 02 · Asukkaat (A:n selaus)

**Tarkoitus**: Selailla tunnetuja henkilöitä — asukkaat, hakijat, entiset asukkaat — kun käyttäjällä on jo nimi tai osoite mielessä.

### Layout

```
┌─ Topbar ────────────────────────────────┐
│ Asukkaat                          ⋮ ▽   │   ← filter-icon
├─────────────────────────────────────────┤
│ 🔍 Hae nimellä, osoitteella…            │   ← .ac-search
├─────────────────────────────────────────┤
│ AE  Aalto, Eero               Sopimus> │
│     Asukas · M-katu 8 B 12             │
├─────────────────────────────────────────┤
│ AH  Ahonen, Eero        Allekirjoitus> │
│     Hakija · tarjous odottaa           │
├─────────────────────────────────────────┤
│ ...                                      │
└─────────────────────────────────────────┘
```

### Search-bar

```
padding:          8px 16px 12px
border-bottom:    1px solid #f1f5f9
display:          flex; align-items: center; gap: 8px

  .input
    flex: 1
    border: 1px solid #e2e8f0
    background: #f8fafc
    padding: 10px 12px
    border-radius: 8px
    font: 400 14px Inter; color: #0f172a
    min-height: 40px
    placeholder color: #94a3b8
```

### Person-row

```
padding:          12px 16px
border-bottom:    1px solid #f1f5f9
min-height:       64px
display:          flex; align-items: center; gap: 12px

  .avatar          40×40 circle, bg: #e6ecf5, color: #002663
                   font: 500 13px Inter, initials (2 kirjainta)
  .info
    .name          font: 500 14px Inter; color: #0f172a
    .meta          font: 400 12px Inter; color: #64748b
  .tag (optional, ennen chevronia)
                   font: 500 10px Inter; padding: 3px 8px; border-radius: full
                   default: bg #f1f5f9, color #475569
                   variant cta:  bg #fdedf4, color #ca2774
                   variant info: bg #d8eef1, color #1c808f
  .chev            16px chevron-right, color: #cbd5e1
```

### Tilat

- Aktivoituja filttereitä näytetään hakukentän alapuolella chip-muodossa
- Tyhjä tila: "Ei osumia" + ehdotettu seuraava toimi (Lisää uusi hakija)
- Lista vaatii virtualisoinnin (DevExtreme `dx-list` tai Angular CDK virtual scroll), koska tuotantodatassa voi olla 5000+ riviä

---

## 03 · Lisää (settings + moduulit)

**Tarkoitus**: Long-tail-näkymä, johon mahtuu kaikki mitä ei kuulu jonoon eikä päätabbeihin.

### Layout

```
┌─ Topbar ────────────────────────────────┐
│ Lisää                                    │
├─────────────────────────────────────────┤
│ ┌─ Profile card (navy) ───────────────┐ │
│ │ AK  Anna Korhonen                   │ │
│ │     Asiakaspäällikkö · Helsinki E.  │ │
│ └─────────────────────────────────────┘ │
│                                          │
│ AINEISTO                                 │
│ ┌─────────────────────────────────────┐ │
│ │ 📄 Tarjoukset            12 avointa │ │
│ │ 🏢 Asunnot vapaana              34  │ │
│ │ 📅 Esittelykalenteri              > │ │
│ │ 🔧 Remontit                       > │ │
│ │ 📷 Valokuvaukset                  > │ │
│ └─────────────────────────────────────┘ │
│                                          │
│ SOVELLUS                                 │
│ ┌─────────────────────────────────────┐ │
│ │ Teema                       Vaalea  │ │
│ │ Kieli                        Suomi  │ │
│ │ Sijaintipalvelut          Sallittu  │ │
│ │ Ilmoitukset             3 tyyppiä   │ │
│ └─────────────────────────────────────┘ │
│                                          │
│ [ Kirjaudu ulos ]                        │
│                                          │
│ Lumo mVasu · v0.1.0                      │
└─────────────────────────────────────────┘
```

### Me-card (profile)

```
padding:          12px
background:       #002663 (navy primary)
color:            #ffffff
border-radius:    12px
margin-bottom:    16px
display:          flex; align-items: center; gap: 12px

  .avatar          48×48 circle, bg: #ffffff22, color: #fff
                   font: 500 14px Inter, initials
  .name            font: 600 16px Playfair Display
  .meta            font: 400 12px Inter; color: rgba(255,255,255,0.67)
```

### Eyebrow + list-group

```
.ds-eyebrow        font: 500 11px Inter; uppercase; letter-spacing: 0.08em
                   color: #94a3b8; margin-bottom: 8px

.more-list         bg: #fff; border: 1px solid #e2e8f0; border-radius: 12px
                   overflow: hidden; margin-bottom: 16px

  .more-row        padding: 12px 14px; min-height: 48px
                   border-bottom: 1px solid #f1f5f9
                   display: flex; align-items: center; gap: 12px
                   icon: 18px stroke-2, color: #475569
                   .label flex: 1; font: 400 14px Inter
                   .value font: 400 13px Inter; color: #64748b
                   .chev  16px; color: #cbd5e1
```

### Toiminnot

- "Tarjoukset" → reitti `/contracts/offers`
- "Asunnot vapaana" → reitti `/units?status=vacant`
- "Esittelykalenteri" → reitti `/calendar`
- "Teema" → opens theme-picker bottom sheet (vaalea / tumma / järjestelmä)
- "Kirjaudu ulos" → confirmation modal, sitten `/login`

---

## 04 · Tehtävän tarkka näkymä

**Tarkoitus**: Yhden tehtävän koko konteksti — asiakas, toiminnot, muistiinpanot, ensisijainen CTA alalaidassa.

### Layout

```
┌─ Topbar ────────────────────────────────┐
│ ← Tutustumiskäynti                      │
├─────────────────────────────────────────┤
│ ⏱ Tänään klo 09:00 · 3 h kuluttua      │   ← hero-time pill
│                                          │
│ Mannerheimintie 12 A 4                   │   ← hero-title H1
│ 2 h, 47 m² · vapaa 1.7. alkaen          │   ← hero-sub
├─────────────────────────────────────────┤
│ ASIAKAS                                  │
│ ┌─────────────────────────────────────┐ │
│ │ VM  Maija Virtanen          📞     │ │
│ │     044 123 4567 · maija.v@…       │ │
│ └─────────────────────────────────────┘ │
│                                          │
│ TOIMINNOT                                │
│ ┌─────────────────────────────────────┐ │
│ │ Avaa kohde Lumo Verkossa          > │ │
│ │ Tee tarjous tästä                 > │ │
│ │ Merkitse pidetyksi                > │ │
│ │ Peruuta käynti                    > │ │   ← danger color
│ └─────────────────────────────────────┘ │
│                                          │
│ MUISTIINPANOT                            │
│ ┌─────────────────────────────────────┐ │
│ │ Asiakas on muuttamassa Tampereelta… │ │
│ └─────────────────────────────────────┘ │
├─────────────────────────────────────────┤
│ [        Avaa kohde        ]            │   ← .ac-footer-cta
└─────────────────────────────────────────┘
```

### Hero-time pill

```
display:          inline-flex; align-items: center; gap: 6px
font:             500 12px Inter
color:            #002663
background:       #e6ecf5
padding:          4px 10px
border-radius:    full
prefix-icon:      clock 16px
```

### Hero-title

```
font: 600 26px Playfair Display
color: #0f172a
margin: 10px 0 4px
line-height: 1.15
```

### Contact-card / row-action

Spec on samanlainen kuin `more-list` mutta `row-action` on klikattava button-elementti. `row-action.danger`-tilassa teksti on `#a32d2d`.

### Footer-CTA

```
padding: 12px 16px (+ env(safe-area-inset-bottom))
background: #fff
border-top: 1px solid #e2e8f0

  .btn-cta         bg: #ca2774; color: #fff; border: 0
                   padding: 12px 20px; min-height: 48px; width: 100%
                   border-radius: 8px
                   font: 500 15px Inter
                   hover: bg #a81e60
                   active: bg #871650
```

### Tilat

- "Merkitse pidetyksi" → confirmation modal "Käynti pidetty?" → näkymä päivittyy, kortti poistuu jonosta
- "Peruuta käynti" → reasonpicker bottom sheet ennen vahvistusta
- Note-block on inline-editoitava (tap → tekstikenttä laajenee)

---

## 05 · Pikahaku (Quick Search)

**Tarkoitus**: Long-tail-pääsy mihin tahansa entiteettiin tai toimintoon yhdellä kentällä. Tämä on Cmd+K-ekvivalentti mobiilille.

### Layout

```
┌─ Quick-search bar ──────────────────────┐
│ 🔍 manner|                       Peru   │
├─────────────────────────────────────────┤
│ KOHTEET                                  │
│ 🏢 [Manner]heimintie 12 A 4              │
│    2 h · 47 m² · vapaa 1.7.             │
│ 🏢 [Manner]heimintie 12 B 7              │
│    3 h · 68 m² · varattu                │
│                                          │
│ ASUKKAAT                                 │
│ VM  Virtanen, Maija                      │
│     Hakija · [Manner]…ie 12 A 4         │
│                                          │
│ TOIMINNOT                                │
│ ➕ Tee uusi tarjous                      │
│    Aloita tyhjältä lomakkeelta          │
│ 📅 Varaa esittely                        │
│    Sovi aika hakijan kanssa             │
└─────────────────────────────────────────┘
```

### Quick-search bar

```
padding: 12px 16px
border-bottom: 1px solid #f1f5f9

  input
    flex: 1
    border: 0
    background: #f1f5f9
    padding: 10px 14px
    border-radius: full
    font: 400 15px Inter
    autofocus: true

  .qs-cancel
    background: transparent
    border: 0
    color: #002663
    font: 500 14px Inter
```

### Hakuosumarivit

```
.qs-section        font: 500 11px Inter; uppercase; letter-spacing: 0.08em
                   color: #94a3b8; padding: 14px 16px 6px

.qs-row            padding: 10px 16px; min-height: 56px
                   border-bottom: 1px solid #f8fafc
                   display: flex; align-items: center; gap: 12px

  icon (left)      18px stroke-2; color: #475569
  .info
    .title         font: 500 14px Inter; color: #0f172a
    .meta          font: 400 12px Inter; color: #64748b

  mark             background: #fdedf4; color: #ca2774
                   padding: 0 2px; border-radius: 2px
                   (hakutermin highlight)
```

### Käyttäytyminen

- **Debounce**: 200 ms
- **Min query length**: 2 merkkiä
- **Tulosryhmät** järjestyksessä: Kohteet → Asukkaat → Sopimukset → Toiminnot
- **Arrow-up/down**: navigoi rivien välillä, Enter avaa
- **Escape tai "Peru"**: sulkee haun, palaa edelliseen näkymään
- **Recent queries**: tyhjällä kentällä näytetään 5 viimeisintä hakua

### Avaaminen

- Tehtävät-näkymän hakuikoni
- Mobiili: tämä on koko ruudun overlay, ei modal
- Desktop (jos PWA avataan laajemmalla viewportilla): Cmd+K avaa keskitetyn 720px-modalin

---

## Liitetyt referenssit

- Live HTML-prototyyppi: `prototype/A+C navigation.html`
- React-komponentit (visuaalinen viite, ei tuotantokoodi): `prototype/screens.jsx`
- CSS-muuttujat: `tokens.css`
