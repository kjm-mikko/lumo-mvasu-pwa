# Navigaatiorakenne — koko mVasu (XAF Model.xafml -lähteestä)

> **Lähde**: `Model.xafml` (XAF Application Model). Tämä on **virallinen totuus** — se mitä XAF generoi käyttöliittymäksi. Mikä tahansa mobiili A+C navigaatio rakennetaan tämän päälle.

`Model.xafml`-tiedosto on liitteenä (`design_handoff_mvasu_navigation/Model.xafml`). Avaa Visual Studiossa Model Editorissa parhaaseen luettavuuteen.

---

## 1. Yleisrakenne

```xml
<NavigationItems NavigationStyle="Accordion" StartupNavigationItem="">
```

- **Accordion-tyyppi** = sivupalkki, jossa ryhmiä laajennettavissa
- **TabbedMDI** options-rivillä = jokainen avattu näkymä saa oman välilehden ylös
- Käynnistyssivua ei ole asetettu → käyttäjä tippuu ensimmäiseen näkymään

---

## 2. Päätason valikkokohdat

| Idx | Id | Caption | ViewId | Ikoni | A+C-mobiili |
|---|---|---|---|---|---|
| 0 | `xVasuSecuritySystemUser_xVasuSecuritySystemUserTasksFull_ListView` | **Käyttäjän tehtävälista** | sama | `Actions.checklist` | **Tehtävät-välilehti** (alapalkki) |
| 1 | `WelcomePage` | **Koti** | `Tiskilista_ListView_DetailView` ⚠️ | `app.application-window-home` | **Koti-välilehti** (HomeHubScreen) — huom. nykyisin avaa Tiskilistan |
| 10 | `Tiskilista` | **Tiskilista** | `Tiskilista_ListView_DetailView` | `VapaatAsunnot.VapaatAsunnot` | **Tiskilista-välilehti** (alapalkki) |
| 20 | `Tarjoukset` | **Tarjoukset** | `SopimusVaraus_ListView_DetailView` | `Varaus.varaus_simple` | Lisää-valikko |
| 30 | `Varausesittelyt` | **Varausesittelyt** | `HuoneistoEsittelyTiedot_ListView_Varausesittely_DetailView` | `Huoneisto.meeting` | Lisää-valikko |
| 40 | `Tutustumiskaynnit` | **Tutustumiskäynnit** | `DirectRentalCustomerInspection_ListView_DetailView` | `Huoneisto.inspection` | Tehtävälähde, ei oma välilehti |
| 50 | `Yleisesittelyt` | **Yleisesittelyt** | `HuoneistoEsittelyTiedot_ListView_Yleisesittely_DetailView` | `Actions.brochure` | Lisää-valikko |
| 55 | `Autopaikka` | **Autopaikka** | `Autopaikka_ListView_DetailView` | `Huoneisto.autopaikka` | Lisää-valikko |
| 60 | `Asma` | **ASMA** (ryhmä) | — | `BO_Folder` | Asukkaat-välilehti + Lisää |
| 70 | `Kire` | **KIRE** (ryhmä) | — | `BO_Folder` | Lisää-valikko |
| — | `Default`, `Reports` | poistettu | — | — | — |
| — | `Jobs`, `Perintä` | piilotettu | — | — | — |

### 2a. ASMA-ryhmän alikohdat

| Idx | Id | Caption | ViewId |
|---|---|---|---|
| 10 | `Valokuvaukset` | Valokuvaukset | `HuoneistoTapahtuma_ListView_DetailView` |
| 20 | `Liidit` | Liidit | `Hakemus_Saapuneet_ListView_DetailViev` *(huom. typo "Detailviev")* |
| 30 | `Henkilöt` | Henkilöt | `Henkilo_ListView_Detailview` |
| 40 | `Allekirjoitettavat` | Allekirjoitettavat | `Sopimus_Allekirjoitettavat_ListView_Detailview` |
| 50 | `Sopimukset` | Sopimukset | `Sopimus_Allekirjoitetut_ListView_10v_Detailview` |
| 60 | `ElectricalSigns` | Sähköiset allekirjoitukset | `Allekirjoitustapahtuma_ListView_Detailview` |
| 70 | `DirectRentalListViewMyyntineuvottelija` | Lumo Verkkokauppa | `DirectRental_ListView_Myyntineuvottelija_DetailView` |
| 75 | `SaapuneetIrtisanomiset_ListView_DetailView` | Saapuneet irtisanomiset | sama |
| 80 | `Yhteyshenkilö` | Yhteyshenkilö | `Yhteyshenkilo_ListView` |
| 90 | `Yritys` | Yritys | `Yritys_ListView` |

### 2b. KIRE-ryhmän alikohdat

| Idx | Id | Caption | ViewId |
|---|---|---|---|
| 10 | `Asuinhuoneisto` | Asuinhuoneisto | `AsuinHuoneisto_ListView_DetailView` |
| 20 | `Talousyksikkö` | Talousyksikkö | `Kustannuspaikka_ListView_DetailViev` *(typo)* |
| 30 | `Remontti` | Remontit | `RemonttiTiedot_ListView_DetailView` |

---

## 3. Tehtava-entiteetti — kanoninen määritelmä

**Luokka**: `xVasu.Data.Security.xVasuSecuritySystemUserTask`
**ListView**: `xVasuSecuritySystemUser_xVasuSecuritySystemUserTasksFull_ListView`
**DetailView**: `xVasuSecuritySystemUserTask_DetailView_Tehtava`
**Preview-DetailView**: `xVasuSecuritySystemUserTask_Preview_DetailView`

### 3a. Tehtavalistan kysely (criteria)

```
[Owner] = CURRENTUSERID() OR
[AssignedTo] = CURRENTUSERID() OR
[xVasuUserTaskAdditionalUsers][[UserID.Oid] = CURRENTUSERID()]
```

→ Käyttäjä näkee tehtävän jos: hän loi sen, hänelle on osoitettu, tai hänet on lisätty extra-käyttäjäksi.

### 3b. Tehtavan kentät (täydellinen lista DetailViewstä)

| Kenttä | Caption | Tyyppi | Huom |
|---|---|---|---|
| `Owner` | "Tehtävän kirjaaja" | user-ref | Kuka loi |
| `AssignedTo` | "Tehtävä osoitettu käyttäjälle" | user-ref | Vastuuhenkilö |
| `UserTaskType` | "Tehtävätyyppi" | lookup | Esim. soittopyyntö, allekirjoitus, tutustumiskäynti |
| `Subject` | "Otsikko" | string | Lyhyt otsikko |
| `Priority` | (default) | enum | Low/Normal/High |
| `Status` | (default) | enum | NotStarted/InProgress/Completed/… |
| `StartDate` | (default) | datetime | Aloituspäivä |
| `DueDate` | "Valmis viimeistään" | datetime | Deadline |
| `ReminderTime` | (default) | datetime | Muistutus |
| `DateCompleted` | (default) | datetime | Suoritettu |
| `PercentCompleted` | (default) | int 0–100 | (ei näytetä listassa) |
| `Description` | "Tehtävän kuvaus" | text (long) | Vapaamuotoinen kuvaus |
| `Kohde` | "Liitetty kohde" | kohde-ref | Talo/kiinteistö |
| `Huoneisto` | "Liitetty huoneisto" | huoneisto-ref | Yksittäinen asunto |
| `Sopimus` | "Liitetty sopimus" | sopimus-ref | Yksittäinen sopimus |
| `Huoneistot` | (collection) | huoneisto[] | Useampi huoneisto |
| `Sopimukset` | (collection) | sopimus[] | Useampi sopimus |
| `Kustannuspaikat` | (collection) | kustannuspaikka[] | Useampi kp |
| `xVasuUserTaskAdditionalUsers` | "Tehtävään liitetyt käyttäjät" | user[] | Lisäkäyttäjät |
| `IsPrivate` | (default) | bool | Yksityinen |
| `Oid` | (default) | guid | Tunniste |
| `EstimatedWorkHours` / `ActualWorkHours` | — | decimal | Piilotettu listassa |

### 3c. ListView-sarakkeet (mitä näkyy listassa)

Järjestyksessä:

1. **Owner.UserName** ("Tehtävän kirjaaja", 138 px)
2. **UserTaskType.Name** ("Tehtävätyyppi", 144 px)
3. **Subject** ("Otsikko", 160 px)
4. **AssignedTo** ("Osoitettu käyttäjälle")
5. **DueDate** ("Valmis viimeistään")
6. **Priority**
7. **Status**
8. **StartDate**
9. **ReminderTime**
10. **DateCompleted**
11. **Kohde** ("Liitetty kohde")
12. **Huoneisto** ("Liitetty huoneisto")
13. **Huoneisto.Huoneistotila** (94 px)
14. **Sopimus** ("Liitetty sopimus")
15. **Sopimus.Tilakoodi.TilaKoodiNimi** ("Tila", 183 px)

Piilotettu listassa: `ActualWorkHours`, `EstimatedWorkHours`, `IsPrivate`, `Description`, `Oid`, `PercentCompleted`.

### 3d. DetailView-asettelu (kaksi saraketta)

**Vasen sarake (col1):**
1. Owner
2. AssignedTo
3. UserTaskType
4. Subject
5. Priority
6. Kohde
7. Huoneisto
8. Sopimus

**Oikea sarake (col2):**
1. Status
2. StartDate
3. DueDate
4. ReminderTime
5. DateCompleted

**Alla:** Description (kuvauskenttä) + välilehti "Tehtävään liitetyt käyttäjät" (`xVasuUserTaskAdditionalUsers`).

---

## 4. Mapping mobiilin A+C-suunnitelmaan

### 4a. Alapalkin neljä välilehteä

```
┌─────────────────────────────────────────┐
│  Tehtävät  │  Tiskilista │ Asukkaat │ + │
└─────────────────────────────────────────┘
```

| Mobiili-välilehti | Lähde XAF-mallissa | Suodatus mobiilissa |
|---|---|---|
| **Tehtävät** | `xVasuSecuritySystemUser_…TasksFull_ListView` | sama criteria + `Status != Completed` + ryhmittele *Tänään / Tällä viikolla / Myöhemmin* |
| **Tiskilista** | `Tiskilista_ListView_DetailView` (myös Koti) | aluetoimisto-/käyttäjäfiltteri |
| **Asukkaat** | `Henkilo_ListView_Detailview` | hakukenttä päällä |
| **Lisää (+)** | kaikki muut päätason kohdat + ASMA + KIRE | kaksitasoinen lista (ryhmä → kohde) |

### 4b. Lisää-valikon hierarkia

```
+ Lisää
├── Tarjoukset
├── Varausesittelyt
├── Tutustumiskäynnit
├── Yleisesittelyt
├── Autopaikat
├── ASMA
│   ├── Valokuvaukset
│   ├── Liidit
│   ├── Allekirjoitettavat
│   ├── Sopimukset
│   ├── Sähköiset allekirjoitukset
│   ├── Lumo Verkkokauppa
│   ├── Saapuneet irtisanomiset
│   ├── Yhteyshenkilöt
│   └── Yritykset
└── KIRE
    ├── Asuinhuoneistot
    ├── Talousyksiköt
    └── Remontit
```

(Henkilöt on jo Asukkaat-välilehdellä, joten ASMA → Henkilöt on poistettu duplikaattina mobiilista.)

### 4c. Tehtavakortti mobiilissa — kenttäkartta

```
┌──────────────────────────────────────┐
│ {UserTaskType.Name}        [Status]  │ ← rivi 1: tyyppipilli + tila
│ {Subject}                            │ ← rivi 2: otsikko (bold)
│                                      │
│ {Huoneisto.Osoite ?? Kohde.Nimi}     │ ← rivi 3: liitetty kohde
│ {Sopimus.Tilakoodi.TilaKoodiNimi?}   │
│                                      │
│ Valmis: {DueDate}    {Priority-ico}  │ ← rivi 4: deadline + prioriteetti
└──────────────────────────────────────┘
```

Avaa tap → DetailView-vastine, mutta mobiili-asettelussa: yksi sarake, accordionit Description + välilehdistä.

---

## 5. Tärkeät huomiot toteutusta varten

1. **`WelcomePage` (Koti) osoittaa nykyisin Tiskilistaan** — A+C ehdottaa että mobiili **Koti** näyttäisi sen sijaan tehtäväkokoavan dashboardin. Tämä vaatii **uuden ViewId**:n esim. `MobileHomeHub_DashboardView` tai puhtaasti REST-pohjaisen näkymän.
2. **Liidit-valikkokohta** käyttää ViewId `Hakemus_Saapuneet_ListView_DetailViev` (typo "Detailviev"). Älä korjaa typoa nyt — säilytä yhteensopivuus.
3. **Tehtava-entiteetti elää `Security`-namespaceissa**, ei `Asma`/`Asutus`-puolella. Tämä viittaa siihen että XAF luo sen `SecuritySystemUserTask`-perusluokasta. mVasu-laajennukset (Kohde/Huoneisto/Sopimus-relaatiot, Kustannuspaikat) on lisätty perinnällä.
4. **`Tiskilista` AppearanceRule**: rivit joilla `vapautuu <= Today()` → AliceBlue-tausta. Tämä on **olemassaoleva visuaalinen signaali** jonka pitää siirtyä myös mobiiliin (esim. "Vapaa nyt" -pilli).
5. **`Hakemus`-luokassa** on tämä huomioitavaa:
   - `HakijaNumero` = "HN" -caption
   - Liidi-tunnistus: `Hakemustyyppi.HakemustyyppiId = 4`
   - Verkkokauppaliidi: tyyppi 5 tai 6 (Eparoija/Keskeyttaja)
6. **AppearanceRule-säännöt** ovat tärkeitä: ne määrittelevät esim. milloin kenttä on read-only roolin tai entiteetin tilan mukaan. Mobiilin pitää kunnioittaa samoja sääntöjä.

---

## 6. Seuraavat askeleet — mitä lisätä handoff-pakettiin

- [ ] **Lisää tämä dokumentti README:n viittauksiin** ✅ (tehty)
- [ ] Päivitä `BACKEND.md` käyttämään tarkkoja ViewId-arvoja `/api/tasks`-mappingissa
- [ ] Päivitä `EXISTING_ENTITIES.md`: `Tehtava` → `xVasuSecuritySystemUserTask`
- [ ] Lisää viite tähän dokumenttiin Claude Code -prompteissa: *"Lue NAVIGATION.md ennen kuin koodaat — siinä on virallinen XAF-rakenne ja Tehtava-entiteetin täydellinen kenttäluettelo."*
