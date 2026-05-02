# Handoff: mVasu Navigation (A + B + C)

> **Mihin tämä paketti on tarkoitettu**
>
> Tämä on **suunnittelureferenssipaketti** mVasu PWA -kehittäjille. Bundlessa olevat HTML-prototyypit ovat aikomuksen ja ulkoasun dokumentointia — **eivät kopioitavaa tuotantokoodia**. Tehtäväsi on toteuttaa nämä näkymät kohdekoodikannassa (`kjm-mikko/lumo-mvasu-pwa`, Angular + DevExtreme + XAF/XPO backend) noudattaen olemassa olevia komponentteja, palveluita ja konventioita.

---

## 1. Yleiskuva

Pakkauksessa on **kolme rinnakkaista navigaatiosuuntaa**, joihin design-vaiheessa päädyttiin:

| Suunta | Lyhyesti | Sopii kun |
|---|---|---|
| **A — Bottom tabs** | 5 välilehteä (Asukkaat / Kohteet / Sopimukset / Tehtävät / Lisää). Perinteinen mobiilipattern, jokainen välilehti on entiteettiselain. | Halutaan minimimuutos XAFin käsitemallista; käyttäjä on tottunut moduulinavigaatioon. |
| **B — Hub & spoke** | Etusivu = moduulikortit ("ASMA", "KIRE", "Lumo Verkkokauppa", "Tarjoukset"). Klikkaus avaa moduulin sisäisen näkymän. | Halutaan, että XAF-moduulit jäävät käsitteellisesti näkyviin. Käytännössä lähimpänä nykyistä työpöytää. |
| **C — Tehtäväjono** | Etusivu = päivän työ aikaryhmissä. Ei moduuleja ensimmäisellä kerroksella. Long-tail haun ja "Lisää"-välilehden takana. | Halutaan radikaali työvirtauudistus; backend pystyy heterogeeniseen jonokyselyyn. |
| **A + C** ⭐ | Bottom tabs (A), mutta ensimmäinen välilehti on tehtäväjono (C). Yhdistää selauspolut + työnohjautuvan etusivun. | **Tämä on ehdotettu suunta.** Riittävä radikaali, riittävän turvallinen. |

Pakkauksessa toimitetaan A+C-yhdistelmä toteutettuna (5 näkymää), ja A:n + C:n + B:n osat on dokumentoitu sanallisesti niin että kehittäjä voi rakentaa yksittäiset näkymät tarvittaessa erikseen.

## 2. Fideliteetti

**High-fidelity.** Kaikki värit, fontit, marginaalit ja tilat ovat lopullisia ehdotuksia. Lukemat (hex, px, ms) on tarkoitettu otettavaksi käyttöön sellaisenaan, paitsi:

- **Fontti**: Playfair Display + Inter ovat avoimia substituutteja. Lopullinen brand on **Austin (heading)** + **Graphik (body)**. Lisenssi auki, ks. `design/open-decisions.md` repon puolella → OD-001. Vaihto tapahtuu yhden CSS-muuttujan kautta (`--lumo-font-heading`, `--lumo-font-body`).
- **Aineiston sisältö**: Asiakasnimet, osoitteet ja ajat ovat fiktiivisiä esimerkkejä realistisilla muodoilla.

## 3. Kohdeympäristö

- **Repo**: `kjm-mikko/lumo-mvasu-pwa`
- **Stack** (oletettu, vahvista): Angular + DevExtreme + XAF/XPO backend
- **Tokenit**: `design/lumo-tokens.css` (jo repossa) — käytä näitä, älä keksi uusia hex-arvoja
- **DevExtreme overrides**: `design/lumo-devextreme-overrides.scss` — tähän tulevat brand-säännöt DevExtreme-komponenteille

## 4. Mitä tehdä Claude Code -istunnossa

Aja repon juuressa:

```
claude --continue
> Lue design_handoff_mvasu_navigation/README.md sekä DEVEXTREME.md,
> EXISTING_ENTITIES.md, SCREENS.md, BEHAVIOR.md ja BACKEND.md. Toteuta
> A+C-yhdistelmänavigaatio Angular-projektiin **käyttäen DevExtreme
> Angular -komponentteja** (dx-list, dx-data-grid, dx-popup, dx-form,
> dx-button, dx-tabs, dx-tree-view, dx-text-box, dx-toast jne.) — älä
> kirjoita omia HTML-vastineita kun DevExtreme-komponentti on saatavilla.
> Käytä design/lumo-tokens.css:n CSS-muuttujia ja vie kaikki brand-
> poikkeamat design/lumo-devextreme-overrides.scss:ään. Käytä OIKEITA
> XAF-kenttänimiä (EXISTING_ENTITIES.md), älä BACKEND.md:n esimerkkien
> englanninkielisiä versioita jos ne ovat ristiriidassa.
```

> **Hyvä uutinen**: `Tehtävä` on jo olemassa omana XPO-entiteettinään, ja "Käyttäjän tehtävälista" on jo navigaatiossa. C-suunta (tehtäväjono) ei vaadi uutta entiteettiä — vain backend-aggregaattorin joka yhdistää `Tehtävä` + `Tutustumiskäynti` + `Yleisesittely` + `Varausesittely`. Yksityiskohdat: `EXISTING_ENTITIES.md`.

Suositeltava etenemisjärjestys (jokainen pieni PR):

1. `TaskCardComponent` ja `TasksTabComponent` — pelkkä C, ilman backendiä, mock-datalla
2. `BottomNavComponent` + reititys — A:n runko, navigointi `/tasks`, `/people`, `/units`, `/contracts`, `/more`
3. **A+C** — yhdistä yllä olevat
4. Tehtävän tarkka näkymä (`TaskDetailComponent`) ja pikahaku (`QuickSearchComponent`)
5. Backend-kontraktit — ks. `BACKEND.md`

## 5. Pakkauksen sisältö

```
design_handoff_mvasu_navigation/
├── README.md                       ← olet tässä
├── SCREENS.md                      ← jokainen näkymä piksen tarkkuudella
├── BEHAVIOR.md                     ← interaktiot, animaatiot, tilamuutokset
├── BACKEND.md                      ← XAF/XPO-yhdistelmäkysely tehtäväjonolle
├── DEVEXTREME.md                   ← ⚠️ PAKOLLINEN — DevExtreme-komponenttimappaus & säännöt
├── EXISTING_ENTITIES.md            ← ⭐ ENSISIJAINEN LÄHDE — mappaus oikeisiin XAF-entiteetteihin
├── tokens.json                     ← Style Dictionary -yhteensopivat designtokenit
├── tokens.css                      ← sama CSS-muuttujina (kopio repon design/lumo-tokens.css:stä)
├── prototype/                      ← eläviä HTML-mockuppeja (avaa selaimessa)
│   ├── A+C navigation.html
│   ├── kit.css
│   ├── screens.jsx
│   ├── android-frame.jsx
│   └── design-canvas.jsx
└── screenshots/                    ← PNG jokaisesta näkymästä, viittauspohjana
    ├── 01-tasks.png
    ├── 02-people.png
    ├── 03-more.png
    ├── 04-task-detail.png
    ├── 05-quick-search.png
    ├── _existing-tehtavat.png       ← nykytilan ruutukaappaukset XAF Blazorista
    ├── _existing-tutustumiskaynti.png
    ├── _existing-yleisesittely.png
    └── _existing-varausesittely.png
```

> **Lukemisjärjestys**: aloita README → DEVEXTREME (kova rajoite) → EXISTING_ENTITIES (mitä on jo olemassa) → SCREENS (mitä rakennetaan) → BEHAVIOR → BACKEND. `EXISTING_ENTITIES.md` ohittaa `BACKEND.md`:n SQL-luonnokset jos ristiriita; `DEVEXTREME.md` ohittaa `SCREENS.md`:n HTML-luonnokset komponenttivalinnoissa.

---

## ⚠️ Kova rajoite: DevExtreme

mVasu käyttää **DevExpress DevExtreme** -komponenttikirjastoa kaikkialla. Angular-toteutuksen on käytettävä **`devextreme-angular`** -komponentteja (`dx-list`, `dx-data-grid`, `dx-popup`, `dx-form`, `dx-button`, `dx-tabs`, `dx-tree-view`, `dx-text-box`, `dx-date-box`, `dx-toast` jne.) kaikkialla missä ne ovat saatavilla. Tämä koskee yhtä lailla referenssi-HTML:n näkymiä — älä kopioi `<div class="card">`-rakenteita raakana, vaan toteuta ne `dx-list`:n `itemTemplate`:llä.

Yksityiskohdat ja täydellinen komponenttimappaus: **`DEVEXTREME.md`**.

## 6. Lisenssi ja saatavuus

Sisäinen materiaali, vain Lumo Kodit / Kojamo. Lumo brand assetit kuuluvat Kojamo Oyj:lle.
