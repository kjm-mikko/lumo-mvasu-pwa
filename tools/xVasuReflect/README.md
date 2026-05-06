# xVasuReflect

Pieni reflection-CLI joka lataa `xVasu.Module` NuGet-paketin ja dumppaa
public-propertyt halutuille tyypeille. Käytetään ennen jokaista
domain-vaihetta varmistaaksemme että XAF Model.xafml -dokumentaatio
vastaa runtime-luokkia (kentät voivat olla eri-tyyppisiä, deprecoituja,
tai uudelleennimettyjä eri CI-buildissa).

## Käyttö

### Perusdumppi (XPO-tason properties + key/alias/assoc)

```powershell
# Dumppaa yksittäisen tyypin
dotnet run --project tools/xVasuReflect -- xVasu.Data.Security.xVasuSecuritySystemUserTask

# Dumppaa useita tyyppejä peräkkäin
dotnet run --project tools/xVasuReflect -- xVasu.Data.Kire.Huoneisto xVasu.Data.Kire.Kustannuspaikka

# Listaa namespacen kaikki tyypit
dotnet run --project tools/xVasuReflect -- --list xVasu.Data.Security
dotnet run --project tools/xVasuReflect -- --list xVasu.Data.Kire

# Hae tyyppi nimellä (kun et tiedä namespace:a)
dotnet run --project tools/xVasuReflect -- --find Sopimus

# Listaa metodit + overloads (käytetään API-pintoja kaivaessa, esim. XPO SelectData)
dotnet run --project tools/xVasuReflect -- --methods DevExpress.Xpo.Session SelectData
```

Perusdumpin tulostus: property-nimi, .NET-tyyppi, declaring type, XPO-attribuutit
(`key`, `alias:…`, `assoc:…`, `nonpersistent`, `readonly`).

### XAF-metadatat (DetailView-rakennus PWA:han)

```powershell
# Dump kaikki XAF-attribuutit per property — käytetään detail-näkymän
# kenttien järjestyksen, näkyvyyden ja validoinnin reverse-engineeringiin.
dotnet run --project tools/xVasuReflect -- --xaf-fields xVasu.Data.Asma.Henkilo

# Etsi ViewController-luokat ja niiden Actions joita kohdistuu tyyppiin.
dotnet run --project tools/xVasuReflect -- --xaf-controllers xVasu.Data.Asma.Henkilo

# Diagnostiikka: listaa kaikki ladatut ViewController-luokat (tai filteröidyt).
dotnet run --project tools/xVasuReflect -- --xaf-all-controllers          # 100+ DevExpress-sisäistä
dotnet run --project tools/xVasuReflect -- --xaf-all-controllers Asiakas  # name-filter
```

`--xaf-fields` näyttää per kenttä: `Index`, `Browsable`, `VisibleInDetailView`,
`RuleRequiredField`, sekä summan tärkeistä attribuuteista (`DisplayName`,
`ModelDefault`, `EditorAlias`, `Appearance`, `RuleRange`, …). Class-level
attribuutit dumpataan erikseen (`DefaultClassOptions`, `ImageName`,
`NavigationItem`, `ModelDefault("Caption", …)`, `Appearance`-säännöt).

**Miksi `--xaf-controllers` voi palauttaa 0:n.** xVasu-projektin custom
ViewController-luokat (Impersonate, SMS, Pikavaraus, ATPI Operaatiot yms.)
asuvat Web/Win/Blazor-puolen modulissa (esim. `xVasu.Module.Web.dll`) joka
ei ole tämän työkalun NuGet-ribkkeen alaisuudessa. xVasu.Module-paketti
sisältää persistenttiluokat ja perusservicet, mutta ei UI-puolen
controllers:eja. Action-listan löytäminen vaatii joko:

- Lataa kohdedeployin Web/Win-DLL-tiedosto suoraan tooliin (`Assembly.LoadFrom`)
- TAI tee ristikkäishaku `--xaf-fields`-tulosteesta — Appearance-säännöt
  joiden `AppearanceItemType="Action"` paljastavat action-ID:n
  `TargetItems`-attribuutista (esim. `QueryAtpiDataAction` löytyy Henkilo:n
  appearance-säännöistä).

## A0-vaiheen löydökset (vahvistus NAVIGATION.md §3 vasten)

### `xVasu.Data.Security.xVasuSecuritySystemUserTask` — base `XPObject`

Vahvistetut kentät (täsmäävät NAVIGATION.md:n kanssa):

| Kenttä | Runtime-tyyppi | Huom |
|---|---|---|
| `Owner` | `xVasuSecuritySystemUser` | assoc `xVasuSecuritySystemUserTaskOwner` |
| `AssignedTo` | `xVasuSecuritySystemUser` | |
| `UserTaskType` | `xVasuSecuritySystemUserTaskType` | lookup, OID **Int32** |
| `Subject` | `String` | |
| `Description` | `String` | (text, ei size-kapaa reflectionissa) |
| `Priority` | `Priority` | DevExpress-enum (Low/Normal/High) |
| `Status` | `TaskStatus` | DevExpress-enum |
| `StartDate` | `DateTime` | non-nullable; default 0001-01-01 = "ei asetettu" |
| `DueDate` | `DateTime` | sama logiikka |
| `DateCompleted` | `DateTime` | sama logiikka |
| `IsPrivate` | `Boolean` | |
| `Kohde` | `Kustannuspaikka` | "Liitetty kohde" |
| `Huoneisto` | `Huoneisto` | "Liitetty huoneisto" |
| `Sopimus` | `Sopimus` | "Liitetty sopimus" |
| `Huoneistot` | `IList<Huoneisto>` | M-N kollektio |
| `Sopimukset` | `IList<Sopimus>` | M-N kollektio |
| `Kustannuspaikat` | `IList<Kustannuspaikka>` | M-N kollektio |
| `xVasuUserTaskAdditionalUsers` | `XPCollection<xVasuUserTaskAdditionalUser>` | huom. luokka on **yksikkö** `xVasuUserTaskAdditionalUser`, kollektio monikko |

### Eroavaisuudet NAVIGATION.md vs. runtime

1. **`ReminderTime` ei ole `DateTime`** — se on `PostponeTime` (DevExpress
   custom-tyyppi `nonpersistent` -merkinnällä). Oikea persistettä
   muistutus on **`AlarmTime`** (`Nullable<DateTime>`). Käytettäessä
   muistutusten kyselyyn pitää käyttää `AlarmTime`:a, ei `ReminderTime`:a.

2. **`EstimatedWorkHours` / `ActualWorkHours` ovat `Int32`**, ei `decimal`
   kuten NAVIGATION.md sanoo.

3. **`Oid` (Guid)** ei näy declared-only -dumppauksessa koska XPObject
   periytyminen tuo sen — runtimessa on. Käytä `t.Oid` taskin
   identifierinä (Guid).

4. Runtime-only -kentät joita NAVIGATION.md ei mainitse:
   - `IsPostponed` (Boolean) — task lykätty
   - `RemindIn` (`Nullable<TimeSpan>`) — relatiivinen muistutus
   - `NotificationMessage` (nonpersistent string)
   - `PostponeTimeList` (nonpersistent enumerable)
   - `HuoneistoLinks`, `SopimusLinks`, `KustannuspaikkaLinks` — sisäiset
     M-N -taulut. Käytetään `Huoneistot` / `Sopimukset` /
     `Kustannuspaikat` -helper-collectionia mappauksessa, ei näitä.

### Kyselykriteeri (mobiilin "minulle näkyvät" tehtävät)

NAVIGATION.md §3a:

```
[Owner] = CURRENTUSERID() OR
[AssignedTo] = CURRENTUSERID() OR
[xVasuUserTaskAdditionalUsers][[UserID.Oid] = CURRENTUSERID()]
```

Reflection vahvistaa:
- `xVasuUserTaskAdditionalUser.UserID` (`xVasuSecuritySystemUser`)
- `xVasuUserTaskAdditionalUser.UserTaskId` (`xVasuSecuritySystemUserTask`)
- `xVasuUserTaskAdditionalUser.OID` (Int32 — link-luokan oma key)

### `xVasuSecuritySystemUserTaskType`
- `OID` (Int32, key)
- `Name` (String) — käytetään listalla kuvaamaan tyyppiä
- `Description` (String)

### `xVasuSecuritySystemUser` — relevantit kentät tehtävämappausta varten
- `Etunimi`, `Sukunimi`, `Kokonimi` (PersistentAlias)
- `Email`, `GSM`, `WorkPhone`, `Position`
- `xVasuSecuritySystemUserTaskOwner` — XPCollection ownerina
- `xVasuSecuritySystemUserTasksFull` — IList laskettu täyslista
- `xVasuUserTaskAdditionalUsers` — XPCollection kun käyttäjä on extra

### `xVasu.Data.Kire.Kustannuspaikka` (Kohde) — relevantit kentät
- `Kptunnus` (Int32, key)
- `KpNimi` (String)
- `Osoite`, `Postinumero`, `Postitoimipaikka`
- `Kunta` (assoc Kunta)
- `AlueToimistoTunnus` (assoc AlueToimisto) — käytetään scope-filterissä
- `Header` (PersistentAlias) — yhdistetty näyttöteksti
- `Oletusrakennus` (Rakennus) — sijainti rakennustasolla

### Avoinna jatkoon (A2–A8 kun tarve)

- `xVasu.Data.Asma.Sopimus` — ei vielä reflektoitu, dumppaa kun A2-vaiheessa
  Tarjous-/Sopimusvaraus -tehtävät tulevat
- `xVasu.Data.Kire.Huoneisto` — laaja luokka, dumpattu mutta tulkitsematta;
  käytetään list-kortin Address-stringin generointiin
- `xVasu.Data.Asma.HuoneistoEsittelyTiedot` — Yleisesittely / Varausesittely

## Huomioita

- Nightly NU1902/NU1903 -varoitukset Azure.Identity / Cryptography.Xml
  -paketeista tulevat xVasu.Module:n transitiivisista riippuvuuksista
  ja vaikuttavat myös mVasu.Api:in. Eivät blokkaa toolin käyttöä.
- Tooli ei käynnistä XafApplicationia eikä DB-yhteyttä; pelkkä
  Assembly.LoadFrom + reflection riittää. Nopeampi ja turvallisempi kuin
  test-harness.
