# Open Decisions — mVasu NextGen

Tähän dokumenttiin merkitään suunnitteluvaiheen päätökset jotka ovat **tarkoituksella avoimia tai väliaikaisia** ja vaativat myöhempää uudelleenarviointia. Tarkoitus on, ettei mikään tärkeä päätös unohdu kun aihio kasvaa lopulliseksi sovellukseksi.

Jokainen rivi sisältää: status, konteksti, vaihtoehdot, nykyinen valinta perusteluineen, ja **trigger** — tilanne joka käynnistää uudelleenarvioinnin.

---

## OD-001 · Brändifonttien lisensointi (Austin + Graphik)

**Status:** Avoin
**Päätetty:** 2026-05-01
**Päättäjä:** Suunnitteluvaihe (vahvistus brand-tiimiltä tarvitaan)

### Konteksti
Lumo Visual Identity Guidelines 2025 määrittelee brändifontit:
- Otsikot: **Austin** (Roman / Medium) — Commercial Type
- Body + CTA: **Graphik** (Regular / Medium) — Commercial Type

Molemmat ovat kaupallisia fontteja, jotka vaativat lisenssin per käyttötarkoitus (web, app, desktop). Käytön laajuus mVasun kontekstissa:
- Web font (PWA selaimessa)
- Embedded app font (mahdollinen MAUI Hybrid -versio tulevaisuudessa)
- Käyttäjämäärä: organisaation sisäiset käyttäjät (Apple Business Manager -jakelu)

### Vaihtoehdot
1. **Hankkia/varmistaa Austin + Graphik -lisenssit** koko Kojamo-organisaatiolle ja käyttää virallisia brändifontteja.
2. **Käyttää avoimen lähdekoodin substituutteja** kehitysvaiheen ajan (Playfair Display + Inter), vaihtaa virallisiksi kun lisenssit varmistettu.
3. **Pysyä avoimissa fonteissa pysyvästi** ja päivittää brand guidelines vastaamaan toteutusta.

### Nykyinen valinta
**Vaihtoehto 2** kehitysvaiheessa.

- Otsikot: **Playfair Display Variable** (Google Fonts, SIL Open Font License)
- Body + CTA: **Inter Variable** (Google Fonts, SIL Open Font License)

CSS-fallback-ketju on rakennettu niin, että vaihto Austiniin ja Graphikiin tapahtuu vain päivittämällä ensimmäinen fontti listassa kohdassa `--lumo-font-heading` ja `--lumo-font-body` tiedostossa `lumo-tokens.css`. Ei muita koodimuutoksia tarvita.

### Perustelut
- Avoimet fontit toimivat heti, ei lisenssineuvotteluja
- Inter on visuaalisesti hyvin lähellä Graphikia (humanistic geometric sans)
- Playfair Display on high-contrast didone serif joka säilyttää Austinin "premium serif" -luonteen
- Kehitys ei pysähdy odottamaan lisenssipäätöstä
- Vaihto myöhemmin on kosmeettinen muutos

### Trigger uudelleenarviointiin
- **Ennen tuotantojulkaisua**: brand-tiimin/markkinoinnin hyväksyntä siitä, että avoimet fontit kelpaavat lopulliseen tuotteeseen, TAI päätös hankkia Austin + Graphik -lisenssit.
- **Tehtävä**: selvitä Kojamosta, onko Austin/Graphik-lisenssit jo olemassa muiden brändi-implementaatioiden takia (printtimateriaalit, intranet). Jos kyllä, web-laajennus voi olla pieni lisämaksu eikä koko uusi sopimus.

### Tehtävälista
- [ ] Kysyä brand-tiimiltä: onko Austin + Graphik -lisenssejä Kojamolla web-käyttöön?
- [ ] Jos lisenssit olemassa: hankkia .woff2-tiedostot, sijoittaa `src/assets/fonts/`, päivittää `lumo-tokens.css`
- [ ] Jos lisenssejä ei ole: päättää investoidaanko, vai jatketaanko avoimilla fonteilla
- [ ] Päivittää tämä dokumentti päätöksen jälkeen statuksella "Suljettu"

---

## OD-002 · DevExtreme-teemointistrategia

**Status:** Avoin (väliaikainen päätös tehty)
**Päätetty:** 2026-05-01
**Päättäjä:** Suunnitteluvaihe

### Konteksti
DevExtreme 25.2 tarjoaa kaksi erilaista lähestymistapaa Lumo-brändin sovittamiseen sen komponentteihin:

**Option A — CSS-muuttuja-overridet stockin teeman päälle**
- Käytetään valmista `dx.material.blue.light.css` (tai vastaavaa generic-teemaa) pohjana
- `lumo-tokens.css` overridaa keskeiset värit, fontit, radiukset CSS-muuttujilla
- Lisäkohdat overridataan tarvittaessa specific selectoreilla (esim. `.dx-button-mode-contained.dx-button-default { background: var(--lumo-cta); }`)

**Option B — Custom theme ThemeBuilderilla**
- DevExtremen ThemeBuilder-työkalu (web-pohjainen tai CLI) tuottaa kokonaan custom-teema-CSS:n
- Kaikki värit, fontit, radiukset tehdään ThemeBuilderissa
- Ulostulo: erillinen `lumo-theme.css` jota Angular-sovellus käyttää
- ThemeBuilder-konfiguraatio talletetaan repoon JSON-tiedostona

### Vaihtoehtojen vertailu

| Kriteeri | Option A (overridet) | Option B (ThemeBuilder) |
|---|---|---|
| Aloitusnopeus | ⚡ Nopea — toimii heti | 🐢 Hitaampi — ThemeBuilder-konfiguraatio ensin |
| Iterointinopeus | ⚡ Erittäin nopea — muuta muuttuja, näe heti | 🐢 Vaatii uudelleenkäännön per muutos |
| Brand-truth | ✅ Yksi totuus (lumo-tokens.css) | ⚠️ Kaksi totuutta (tokens + ThemeBuilder-konfiguraatio) |
| Komponenttikattavuus | ⚠️ Osa komponenteista vaatii specific overrideja | ✅ Kaikki komponentit tyylitellään natiivisti |
| Suorituskyky | ⚠️ CSS-cascade vähän raskaampi | ✅ Esikäännetty, optimaalinen |
| DevExtreme-päivitysten kestävyys | ⚠️ Overridet voivat rikkoutua | ✅ ThemeBuilder päivittyy stockin mukana |
| Ylläpitotaakka | ✅ Pieni — yksi CSS-tiedosto | ⚠️ Suurempi — ThemeBuilder-build-pipeline |

### Nykyinen valinta
**Option A — CSS-muuttuja-overridet** aihiovaiheessa.

```scss
// src/styles.scss
@import 'devextreme/dist/css/dx.material.blue.light.css';
@import '@fontsource-variable/inter';
@import '@fontsource-variable/playfair-display';
@import 'assets/styles/lumo-tokens.css';
@import 'assets/styles/lumo-devextreme-overrides.scss';
```

Overrides-tiedosto kohdistuu vain niihin DevExtreme-komponentteihin joita aihiossa käytetään: `dx-button`, `dx-text-box`, `dx-radio-group`, `dx-form`, `dx-load-indicator`. Lopullisen sovelluksen kasvaessa lista kasvaa, ja siinä vaiheessa Option B:n hyödyt voivat alkaa ylittää sen kustannukset.

### Perustelut
- **Iterointinopeus** on tärkeintä aihiovaiheessa kun design vielä elää
- **Yksi totuus** brand-värille (lumo-tokens.css) yksinkertaistaa designerin ja kehittäjän kommunikaatiota
- **Pieni komponenttipinta-ala** aihiossa tekee overrides-määrästä hallittavan (alle 10 komponenttia)
- **Migraatio Option B:hen on suoraviivaista** — kun ThemeBuilder-pipeline rakennetaan, samat token-arvot syötetään sinne

### Trigger uudelleenarviointiin
**Vaihda Option B:hen kun mikä tahansa seuraavista täyttyy:**
1. Override-tiedosto kasvaa yli **300 riviä** tai **15 eri komponenttia**
2. DevExtreme-päivitys (esim. 25.2 → 26.1) rikkoo merkittävän määrän overrideja
3. Suorituskykyongelmia havaitaan render-vaiheessa (Lighthouse Performance < 80 mobiilissa)
4. Brand-tiimi haluaa hienosäätää komponentteja niin tarkasti, että CSS-overridet eivät enää kata vaatimuksia

### Tehtävälista
- [ ] Luo `src/assets/styles/lumo-devextreme-overrides.scss` aihion frontend-vaiheessa
- [ ] Dokumentoi yllä mainitut triggerit erilliseen muistutukseen tiimin retroon
- [ ] Aihiovaiheen lopussa: review tämän tiedoston tila, päivitä rivimäärä-trigger todelliseen havaintoon perustuen

---

## OD-003 · Solution-nimi (placeholder)

**Status:** Avoin
**Konteksti:** Aiemmassa promptissa käytetty `mVasu.NextGen` mutta tämä on suunnittelija-arvaus, ei vahvistettu nimi.
**Päätös ennen ensimmäistä committia.**

---

## OD-004 · Email-claim ja JIT-provisiointi (odottaa nykyisen mVasun provider-koodia)

**Status:** Blocking — ei voi koodata authentication-osuutta ennen kuin selvitetty
**Konteksti:** Backend tarvitsee tietää tarkalleen miten nykyinen mVasu mappaa Entra ID -tokenista käyttäjään (mikä claim, mikä XPO-luokka, JIT-säännöt).
**Tehtävä:** Käyttäjä toimittaa relevantin lähdekoodin nykyisestä mVasusta.

---

## OD-006 · Sijaintipalvelujen toteutus

**Status:** Päätetty (toteutetaan aihion alusta lähtien)
**Päätetty:** 2026-05-01
**Päättäjä:** Suunnitteluvaihe

### Konteksti
Sijaintipalvelut (Geolocation) tarvitaan **jo aihion alkuvaiheessa**, jotta infrastruktuuri on valmiina kun domain-näkymät (kohteet, lähimmät vapaat asunnot, kartta-haut) lisätään myöhemmin.

### Tekniset päätökset
- **Frontend (PWA, selain)**: standardi `navigator.geolocation` API (HTTPS-konteksti tarvitaan, PWA:lla on jo)
- **Tulevaisuus (MAUI Hybrid)**: `Microsoft.Maui.Devices.Sensors.IGeolocation` — sama abstraktio Angular-puolella, eri toteutus laitteilla
- **Lupien käsittely**: kolme tilaa — "Sallittu", "Estetty", "Ei pyydetty"
- **Pakollisuus**: ei pakollista; sovellus toimii ilman sijaintia, mutta toiminnot jotka tarvitsevat sijaintia ovat poissa käytöstä

### Toteutus aihiossa
- Angular `LocationService` reactive permission-statella (signal-pohjainen)
- Asetukset-sivulla "Sijaintipalvelut"-osio (toggle + tila + ohjeteksti)
- Backend DTO `UserLocationDto { latitude, longitude, accuracy, recordedAt }` valmiina
- Endpoint `POST /api/me/location` (optionaalinen — vain jos käyttäjä on hyväksynyt sijainnin tallennuksen)
- Privacy-notice käyttäjälle: lyhyt selitys mihin sijaintia käytetään (GDPR-perusteltavuus)

### GDPR-näkökulma
Sijaintidata on henkilötietoa GDPR-mielessä kun se yhdistetään tunnistettavissa olevaan käyttäjään. mVasun käyttäjäkunta on autentikoitu (Entra ID), joten sijaintidata on aina yhdistettävissä henkilöön.

- Käsittelyperuste: rekisteröidyn suostumus (Article 6(1)(a))
- Säilytysaika: päätetään ennen tuotantoa (suositus: enintään 30 päivää viimeisestä käytöstä)
- Käyttäjän oikeudet: näytä, muuta, poista — toteutetaan Asetukset-sivun kautta

### Trigger uudelleenarviointiin
- Ennen tuotantoa: tietosuojavastaavan vahvistus käsittelyperusteesta ja säilytysajasta
- Kun ensimmäinen kartta-pohjainen näkymä toteutetaan: review käyttäjäkokemuksesta (lupakyselyn ajoitus, fallback ilman sijaintia)

---

## OD-007 · Wordmark-kirjoitusasu (Lumo / lumo)

**Status:** Päätetty
**Päätetty:** 2026-05-01
**Päättäjä:** Brand-ohje (käyttäjän vahvistus)

### Konteksti
Suunnitteluvaiheen mockupeissa wordmark näkyi "lumo" pienillä — typografisesti yleinen wordmark-tyyli Playfair Displayssa. Brand-ohje kuitenkin määrittää että **Lumo kirjoitetaan AINA isolla alkukirjaimella**.

### Päätös
Wordmark = **"Lumo"** (iso L). Sama koskee kaikkea sovelluksen tekstiä: UI-stringit, README, koodikommentit, commit-viestit, dokumentaatio.

### Sovellettava sääntö
- ✓ "Lumo Kodit IT"
- ✓ "Lumo mVasu"
- ✓ "© Lumo Kodit IT"
- ✗ "lumo" (paitsi historiallisissa tiedostonimissä joita ei voi muuttaa)

### mVasu-kirjoitusasu
Erillinen sääntö: **mVasu kirjoitetaan tarkalleen näin** — pieni m, iso V, loput pieniä. Tämä on tuotteen virallinen nimi.
- ✓ "mVasu"
- ✗ "Mvasu", "MVasu", "mvasu", "M-Vasu"

---

## OD-005 · Tietosuoja- ja käyttöehto-linkit

**Status:** Lykätty
**Päätetty:** 2026-05-01
**Päättäjä:** Suunnitteluvaihe

### Konteksti
Login-sivun footerissa oli alkuperäisessä luonnoksessa linkit:
- Tietosuoja
- Käyttöehdot

Päätös tehtiin pudottaa nämä pois ja käyttää yksinkertaistettua "© Lumo Kodit IT · v0.1.0" -footeria, koska kyseessä on **organisaation sisäinen LOB-sovellus** (Apple Business Manager / Managed Google Play -jakelu, ei julkista App Store -listausta).

### Avoin kysymys
Vaatiiko sisäinen mVasu omat tietosuoja- ja käyttöehto-linkkinsä, vai riittääkö Kojamon organisaatiotason tietosuoja-asiakirja (intranet)?

### Huomioitavaa
- **GDPR**: henkilötietoja käsittelevä järjestelmä vaatii rekisteriselosteen ja informoinnin tietosuojasta, mutta nämä voivat olla organisaatiotason dokumenteissa eikä per-sovellus
- **Apple Business Manager / Managed Google Play**: vaatii yksityisyyskäytäntö-URL:n appin metatietoihin (vaikkei näkyisi käyttöliittymässä) — tämä lasketaan erikseen
- **Tulevaisuus**: jos mVasu laajenee Lumo Kotien ulkopuolisille käyttäjille (esim. asukkaat itse, kumppaniyritykset), oma tietosuoja + käyttöehdot tulevat pakollisiksi

### Trigger uudelleenarviointiin
1. **Ennen ensimmäistä tuotantojulkaisua**: Kojamon tietosuojavastaava varmistaa, riittääkö organisaatiotason dokumentaatio mVasun kontekstiin
2. **Apple Business Manager / Managed Google Play -submission**: tarkista mitä metatietoja jakelukanava vaatii (yksityisyyskäytäntö-URL on yleensä pakollinen, vaikkei UI:ssa näkyisikään)
3. **Käyttäjäkunnan laajeneminen** Lumo Kotien ulkopuolelle

### Tehtävälista
- [ ] Selvitä Kojamon tietosuojavastaavalta: riittääkö organisaatiotason tietosuoja vai tarvitaanko per-sovellus-versio
- [ ] Selvitä jakelukanavien metatieto-vaatimukset ennen App Store / Google Play -submissionia
- [ ] Jos linkit lisätään: päätä pitäisikö ne näkyä myös sovelluksen sisällä (esim. Asetukset → Tietoja-sivulla) eikä vain login-sivulla

---

## OD-009 · Käyttäjäasetusten tallennusmuoto (kentät vs. JSON blob)

**Status:** Päätetty (hybridi)
**Päätetty:** 2026-05-02
**Päättäjä:** Toteutusvaihe 5

### Konteksti

xVasun `xVasuSecuritySystemUser`-luokassa ei ole mVasun käyttäjäkohtaisia asetuskenttiä (`PreferredName`, `Theme`, `Language`, `LocationConsent`). Asetukset pitää tallentaa erillisenä luokkana, ja kysymys oli kuinka strukturoida niitä:

1. **Erilliset tyypitetyt kentät** kullekin asetukselle
2. **Yksi JSON blob -kenttä** joka sisältää kaikki asetukset
3. **Hybridi**: tyypitetyt kentät known-tärkeille, JSON blob laajennuksia varten

### Nykyinen valinta

**Vaihtoehto 3 — hybridi** [`MVasuUserSettings`](../src/mVasu.Api/Domain/MVasuUserSettings.cs)-luokassa:

- `PreferredName` (string?) — näkyy UI:ssa (greeting), helppo lukea/näyttää
- `Theme` (string) — yksinkertainen enum-tyyli, default `"light"`
- `Language` (string) — yksinkertainen enum-tyyli, default `"fi"`
- `LocationConsent` (bool) — backend käyttää ehtona ennen sijainnin tallentamista, **on oltava queryable**
- `SettingsJson` (string?, unlimited) — vapaaehtoinen blob tulevia asetuksia varten

### Perustelut

- `LocationConsent` käytetään palvelimen logiikassa (POST /api/me/location -filtterinä) → ehdoton että queryable, ei JSON-purkua per request
- `Theme` ja `Language` näkyvät myös Vasu Blazor -puolen XAF-näkymissä (jos joku haluaa hallinnoida käyttäjäkohtaisesti) → tyypitetyt kentät rendautuvat XAF:ssa automaattisesti
- `SettingsJson` jättää oven auki future-asetuksia varten ilman migraatiota: notification preferences, sarakeasetukset DataGridissa, recent searches, suosikit ulkopuolisille resursseille
- Migraatio JSON:iin → erillisiin kenttiin myöhemmin on suoraviivaista jos joku asetus näyttää ansaitsevan oman sarakkeensa

### Trigger uudelleenarviointiin

- Kun `SettingsJson` kasvaa yli 2–3 ainutlaatuiseen "kenttään" → harkitse niiden nostamista omiin sarakkeisiin
- Jos backend tarvitsee suorittaa hakuja jollain JSON-kentän arvolla → on aika nostaa kenttä erilliseksi

---

## OD-010 · XAF-hostaus mVasu.Api:ssa (XafApplication vs. XPO suora)

**Status:** Päätetty
**Päätetty:** 2026-05-02
**Päättäjä:** Toteutusvaihe 5

### Konteksti

`xVasu.Module` on rakennettu XAF-päälle: sen luokat (`xVasuSecuritySystemUser`, `xVasuSecuritySystemRole`, jne.) toimivat `CompositeObjectSpace.FindObject`:in kautta vain kun ne ovat rekisteröityjä XAF:n business-mallissa. Manuaalinen `XafTypesInfo.Instance.RegisterEntity()` ei riitä — XAF vaatii koko `XafApplication.Setup`-pipelinen joka kerää moduulien `AdditionalExportedTypes`:t ja rakentaa täyden `BusinessClassDescriptor`-metadatan.

Kokeiltiin vaihtoehtoja:

1. **`XafTypesInfo.RegisterEntity` yksittäin** — ei toiminut (sama virhe pysyi)
2. **Assembly-scan + `RegisterEntity`** — ei toiminut
3. **XPO Session-direct** (`((XPObjectSpace)os).Session.FindObject<T>`) — toimi `xVasuSecuritySystemUser`:lle, mutta `MVasuUserSettings` aiheutti `ThreadSafeDataLayer`-lukon (`Cannot modify Dictionary because ThreadSafeDataLayer uses it`). Dynaaminen tyyppi-luonti per-request ei toiminut yhdessä jaetun data layer:in kanssa
4. **Täysi XafApplication-hosting** — toimii

### Nykyinen valinta

**Vaihtoehto 4 — XafApplication-hosting headless-tilassa.** Tämä on XAF:n suunniteltu reitti.

Toteutus:

- [`MVasuApiModule`](../src/mVasu.Api/Domain/MVasuApiModule.cs): `ModuleBase`-aliluokka joka kerää `AdditionalExportedTypes`-listalle kaikki `xVasu.*`-namespacen `PersistentBase`-pohjaiset tyypit (assembly-scan) sekä `MVasuUserSettings`
- [`MVasuApiApplication`](../src/mVasu.Api/Data/MVasuApiApplication.cs): `XafApplication`-aliluokka, joka heittää `NotSupported`-poikkeuksen `CreateLayoutManagerCore`:ssa (UI-managerit eivät kosketa Web API -hostia)
- DI rekisteröi `XafApplication`-singletonin, kutsuu `Setup()` kerran sovelluksen alussa, ja `IObjectSpaceProvider` haetaan applikaatiosta

### Perustelut

- XAF business-model rakentuu kerralla, ei yritetä kiertää sen invariantteja
- Tulevien permission-pohjaisten näkymien (`SecurityStrategyComplex`) liittäminen on suoraviivaista — applikaatio on jo olemassa
- Resolver käyttää tavallista `IObjectSpace.FindObject<T>`:ta — ei erikoiscasteja
- Saadaan XAF:n full meta (associations, validation, conditional appearance) saataville myöhempiä vaiheita varten

### Trigger uudelleenarviointiin

- Jos Setup-pipeline näkyy ongelmana (esim. käynnistysaika, muistinkulutus), harkitaan kevyempää `DevExpress.ExpressApp.AspNetCore`-pakettia jos sellainen julkaistaan tähän skenaarioon
- Jos joskus halutaan poistaa xVasu-riippuvuus kokonaan, palataan suoraan XPO:hon ilman XAF:ia (mutta silloin pitää myös duplikoida domain-luokat)

---

## Päätösten lisäysohje

Uusia päätöksiä lisätään seuraavalla sapluunalla:

```markdown
## OD-NNN · Otsikko

**Status:** Avoin / Päätetty / Suljettu / Blocking
**Päätetty:** YYYY-MM-DD
**Päättäjä:** Henkilö tai vaihe

### Konteksti
Mistä päätöksestä on kyse, mikä tilanteen pakottaa?

### Vaihtoehdot
Numeroidut vaihtoehdot 1, 2, 3...

### Nykyinen valinta
Mikä valittiin

### Perustelut
Miksi tämä, eikä jokin toinen

### Trigger uudelleenarviointiin
Mikä konkreettinen tilanne käynnistää review-prosessin

### Tehtävälista
- [ ] Konkreettiset toimenpiteet
```
