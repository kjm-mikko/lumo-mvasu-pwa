# EXISTING ENTITIES — XAF/XPO Schema Reference

> Tämä dokumentti perustuu **oikeisiin XAF Blazor -ruutukaappauksiin** mVasun nykyversiosta. Se on **ensisijainen lähde** kenttänimille ja relaatioille — `BACKEND.md`:n SQL-luonnokset on linjattava näihin nimiin.
>
> Kuvat: `screenshots/_existing-*.png`

---

## 1. Yhteenveto

Hyvä uutinen: **`Tehtävä` on jo olemassa omana entiteettinään**, ja "Käyttäjän tehtävälista" on jo navigaatiossa. Se mitä tarvitaan, on **aggregaatio**:

```
   Tehtävä  +  Tutustumiskäynti  +  Yleisesittely  +  Varausesittely
        │                                                       │
        └──────────────► /api/tasks (yhdistetty jono) ◄─────────┘
```

Eli C-suunta (tehtäväjono) **ei vaadi uutta entiteettiä** — vain backend-aggregaattorin joka yhdistää nämä neljä lähdettä yhdeksi heterogeeniseksi listaksi.

---

## 2. Tehtävä-entiteetti (jo olemassa)

Lähde: `screenshots/_existing-tehtavat.png`

| Kenttä | Tyyppi | Esimerkki | Käyttö |
|---|---|---|---|
| `Kohde` | dropdown (lookup) | — | Asunto/talo johon tehtävä liittyy |
| `Huoneisto` | dropdown (lookup) | "Malminiityntie 16 B 94, 01350 Vantaa" | Tarkka huoneisto |
| `Sopimus` | dropdown (lookup) | "100029/LEE PHILIP/Vänrikinkatu 2 Varasto" | Sopimusviite |
| `Priority` | enum | `High` (🟧), `Medium`, `Low` | Käyttäjän asettama |
| `Status` | enum | `Not started`, `In progress`, `Done` | Käytössä |
| `Aloitus` | datetime | `7.10.2025 16.00.00` | Aloitusaika |
| `Valmiina` | int (%) | `0` | Valmistumisaste |
| `Tehtävätyyppi` | dropdown | "Remontin tilaus", "Tarkastus", "Hinnoittelu" | Vapaa luokittelu |
| `Otsikko` | string | "Remontin tilaus" | Lyhyt otsikko |
| `Tehtävän kuvaus` | text | "Muistathan käydä testaamassa…" | Pitkä kuvaus |
| `Tehtävän kirjaaja` | user-ref | `VVOAD\koivukai` | Luoja |
| `Osoitettu käyttäjälle` | user-ref | — | Vastuuhenkilö |
| `Liitetyt käyttäjät` | user[] | `VVOAD\koirialau`, `VVOAD\parkkmik`, `VVOAD\vekolasse` | Useampi vastuullinen |
| `Liitetty huoneisto` / `Liitetty kohde` / `Liitetty sopimus` | refs | — | Lisäliitokset |
| `Muistutusaika` | datetime | — | Muistutus käyttäjälle |
| `Huoneistotila` | enum | — | (käyttötarkoitus epäselvä, vahvista) |

**Toiminnot UI:ssa**: `+ New`, `Postpone`, `Save`. Riveillä `Drag a column header here to group by that column` — vakio-DevExtreme-DataGrid.

**Mapping jonoon (`type`):**

- Status `In progress` ja `Aloitus < now` → `accent: 'navy'`, `urgent: false`
- Status `Not started` ja `Priority = High` → `accent: 'cta'`, `urgent: true` jos `Aloitus < now + 24h`
- Status `Done` → ei näytetä jonossa, näkyy "Valmistuneet"-osiossa Lisää-välilehden alla

---

## 3. Tutustumiskäynti-entiteetti (jo olemassa)

Lähde: `screenshots/_existing-tutustumiskaynti.png`

| Kenttä | Tyyppi | Esimerkki | Huom |
|---|---|---|---|
| `Osoite` | string | "Asemakuja 1 B 69 02770 ESPOO" | Kohde |
| `Asiakas` | string (lookup henkilö) | "Huhtikuu Heikki" | |
| `Email` | string | "riikka.ilmakunnas@lumo.fi" | (ristiriita ruutukaappauksessa: ilmoittaa Asiakkaan emailin tai kirjaajan?) |
| `Gsm` | string | "0400104230" | Puhelin |
| `Alkuaika*` | datetime | `10.04.2026 13.00` | Pakollinen |
| `Kesto` | duration | `00:30:00` | |
| `Esittelijä` | user-ref | "Reinilä Liisa" | Vastuuhenkilö |
| `SopimuksenPeruutusViimeistään` | datetime | `10.04.2026 20.00.00` | **Käytä `urgent`-triggerinä** |
| `Kirjaaja` | user-ref | "Reinilä Liisa" | |
| `Peruutettu` | bool | ☐ | Filteröi pois `true` |
| `Käsitelty` | bool | ☐ | Filteröi pois `true` (= valmis) |
| `Remontit` | child-list | — | Liitetyt remontit (ei kuulu jonokorttiin) |

**Mapping jonoon:**

```
WHERE Peruutettu = false
  AND Käsitelty = false
  AND Esittelijä = :user
  AND Alkuaika BETWEEN :from AND :to

→ {
  type: 'visit-introduction',
  accent: 'navy',
  urgent: now > Alkuaika - INTERVAL '2 hours',
  when: { time: format(Alkuaika), sortOrder: epoch(Alkuaika) },
  title: Osoite,
  who: `${Asiakas} · ${Gsm}`,
  meta: `${formatDuration(Kesto)} · esittelijä ${Esittelijä}`,
  actions: [
    { label: 'Avaa kohde', primary: true, kind: 'navigate', payload: '/units/...' },
    { label: 'Soita', kind: 'phone', payload: Gsm },
  ],
  entityRef: { module: 'core', entityType: 'Tutustumiskaynti', id: ... }
}
```

---

## 4. Yleisesittely-entiteetti (jo olemassa)

Lähde: `screenshots/_existing-yleisesittely.png`

| Kenttä | Tyyppi | Esimerkki | Huom |
|---|---|---|---|
| `RuokakuntiaYleisesittelyssä*` | int | `0` | Ilmoittautuneiden määrä |
| `Osoite` | string | "Maauunintie 23 A 2 01450 VANTAA" | |
| `LyhytSelite` | string | "Tervetuloa esittelyyn, tapaaminen A-rapun edessä" | Käytä jonokortin `meta`-kentässä |
| `Kokonimi` | string | — | (jos olemassa, kontaktihenkilö) |
| `Email` | string | — | |
| `Gsm` | string | — | |
| `Esittelyaika*` | datetime | `10.04.2026 13.00` | |
| `Esittelynkesto*` | duration | `00:15:00` | |
| `Sopimustila*` | enum | `Irtisanottu` | Asunnon sopimustila |
| `Tarjouksen muistio` | text | — | tab |
| `Remontit` | child-list | — | tab |

**Mapping jonoon:**

```
WHERE Peruutettu = false
  AND Valmis = false
  AND Esittelijä = :user
  AND Esittelyaika BETWEEN :from AND :to

→ {
  type: 'open-house',
  accent: 'navy',
  urgent: false,                          // yleisesittely ei kiireellinen
  when: { time: format(Esittelyaika), sortOrder: epoch(Esittelyaika) },
  title: Osoite,
  meta: `${RuokakuntiaYleisesittelyssä} ilmoittautunutta · ${LyhytSelite}`,
  actions: [{ label: 'Avaa esittely', primary: true, kind: 'navigate' }]
}
```

**Listanäkymän filteri**: yläosassa on dropdown `Kaikki` → todennäköisesti tila-filtteri (Avoimet / Pidetyt / Peruutetut).

**Käytössä olevat status-kentät** (vahvistettu UI:sta): `Peruutettu`, `Valmis`.

---

## 5. Varausesittely-entiteetti (jo olemassa)

Lähde: `screenshots/_existing-varausesittely.png`

Kuten Yleisesittely, plus:

| Kenttä | Tyyppi | Esimerkki | Huom |
|---|---|---|---|
| `Päähakija` | string (henkilö) | — | Pakollinen — varatun asunnon hakija |
| `Sopimustila` | enum | `Toistaiseksi` | |
| `Varaus` | ref | — | Liittyvä varaus |

Lista on yleensä lyhyt (yhden hakijan kanssa), `RuokakuntiaYleisesittelyssä` ei ole pakollinen.

**Mapping**:

```
type: 'visit-reservation',
accent: 'navy',
urgent: now > Esittelyaika - INTERVAL '2 hours',
title: Osoite,
who: Päähakija,
meta: `Varattu, ${formatDuration(Esittelynkesto)}`
```

---

## 6. Muut näkymät navigaatiossa (todettu, ei vielä mapattu)

Vasemman valikon hierarkia (Tutustumiskäynti-kuvasta):

```
🔍 Filter…
  Käyttäjän tehtävälista     ← ⭐ relevantti, tästä lähtee A+C:n etusivu
  Koti
  Tiskilista                  ← Mappaus: 'desk-list-item' tasks
  Tarjoukset                  ← Mappaus: 'signature-pending' kun valmis
  Varausesittelyt
  Tutustumiskäynnit
  Yleisesittelyt
  Autopaikka
  ASMA ▼
    Valokuvaukset             ← Mappaus: 'photo-scheduled'
    Liidit                    ← Mappaus: 'lead-callback' kun yli 2pv vanha
    Henkilöt
    Allekirjoitettavat        ← Mappaus: 'signature-pending'
    Sopimukset
    Sähköiset allekirjoitukset
    Lumo Verkkokauppa
    Saapuneet irtisanomiset   ← Mappaus: 'inbox-termination'
    Yhteyshenkilö
    Yritys
  KIRE ▼
    Asuinhuoneisto
    Talousyksikkö
    Remontit                  ← Mappaus: 'renovation-approval'
```

**Käyttöliittymänotaatio**: nykyinen vasen-valikko on suoraan XAFin generoima moduulinavigaatio. A+C-suunnan etusivu ei poista tätä — se vain **piilottaa sen ensimmäiseltä kerrokselta**. Vasen valikko tulee näkymään "Lisää"-välilehden takaa tai pikahaun "Toiminnot"-osiossa.

---

## 7. Kerrokset, joita designissa ei vielä ole näkynyt

Ruutukaappauksista nähdyt mutta vielä mappamattomat:

- **Autopaikka** — oma entiteetti, todennäköisesti `Sopimus`-tyyppi. Mahdollinen jonotyyppi: "Autopaikka vapautunut" / "Autopaikkahakemus saapunut"
- **Yritys / Yhteyshenkilö** — B2B-kontekstit. Voivat tulla pikahaun "Asukkaat"-tuloksissa (laajennetaan: "Asukkaat & Yritykset")
- **Talousyksikkö** — kiinteistörekisterin yksikkö, ei henkilötyö → ei kuulu jonoon, vain pikahakuun

---

## 8. Mitä päivittää designissa kuvien perusteella

Päivitetty `BACKEND.md`:n SQL-luonnoksiin (versio 0.2):

- ❌ `Aikataulu` → ✅ `Alkuaika` (Tutustumiskäynti) tai `Esittelyaika` (esittelyt)
- ❌ `Tila IN ('sovittu', 'vahvistamatta')` → ✅ `Peruutettu = false AND Käsitelty = false`
- ❌ `VastuuHenkilo` → ✅ `Esittelijä` (esittelyt) tai `Osoitettu käyttäjälle` (Tehtävä)
- ✅ Lisää **`SopimuksenPeruutusViimeistään`** -kentän käyttö Tutustumiskäynnin urgent-triggerinä — tämä on liiketoiminnallisesti tärkein deadline

`SCREENS.md`:n TaskCardin `meta`-kenttä Yleisesittelylle tulee käyttää **`RuokakuntiaYleisesittelyssä`** + **`LyhytSelite`** sellaisenaan — frontend ei keksinyt "3 ilmoittautunutta · 2 vapaata" -formaattia, se pitää muotoilla oikeista kentistä.

---

## 9. Avoimet kysymykset (ei vielä päätettyjä, vahvista repon kehittäjältä)

| ID | Kysymys |
|---|---|
| OD-006 | Yleisesittelyn `Sopimustila` — mihin se vaikuttaa jonossa? Onko sallittu näyttää "Irtisanottu" julkisesti, vai pitääkö sensuroida? |
| OD-007 | `Tehtävä.Status` arvojen täydellinen listaus — käytetäänkö `Postpone`-toiminnolle omaa statusta vai vain päivitetään `Aloitus`? |
| OD-008 | `Tehtävä.Liitetyt käyttäjät` — jos käyttäjällä on tehtävä `Liitetyt`-listassa mutta ei `Osoitettu`-kentässä, näytetäänkö hänelle? Ehdotus: kyllä, vaaleammalla tagilla "Osallistuja". |
| OD-009 | Tiskilista — onko tämä erillinen entiteetti vai konsolidoitu näkymä useista? |
