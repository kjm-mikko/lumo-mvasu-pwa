# DEVEXTREME — Pakollinen komponenttikirjasto

> ⚠️ **Tämä on kova rajoite, ei suositus.** mVasu käyttää DevExpress DevExtreme -komponentteja kaikkialla nykyisessä XAF Blazor -versiossa. Angular PWA -toteutuksen on käytettävä **DevExtreme Angular** -komponentteja kaikissa kohdissa joissa ne ovat saatavilla. Älä rakenna omia listoja, taulukoita, dialogeja tai lomakekontrolleja jos DevExtremestä löytyy vastaava.
>
> Tämä koskee yhtä lailla A+C-navigaation kaikkia näkymiä — myös tehtäväjonoa, pikahakua ja "Lisää"-näkymää.

---

## 1. Miksi DevExtreme on pakollinen

- **Olemassa oleva järjestelmä** käyttää DevExtremeä kaikkialla — käyttäjä on tottunut DataGridin sarakkeenvalitsijaan, filtterilippuihin, paginointiin
- **Lisenssi on jo maksettu** Lumolla
- **Käyttöoikeudet, lokalisointi, accessibility** on jo testattu DevExtremessä
- **Custom-vastineiden ylläpito** kahdessa kirjastossa olisi tappiollista

## 2. Komponenttimappaus — design → DevExtreme Angular

Käytä näitä **vakio-komponentteina kaikissa A+C-näkymissä**. Älä kirjoita HTML-vastineita.

| Designin elementti | DevExtreme Angular -komponentti | Huom |
|---|---|---|
| Tehtäväkortti / lista (jonossa) | **`dx-list`** ([docs](https://js.devexpress.com/Angular/Documentation/ApiReference/UI_Components/dxList/)) | Ryhmittely (`grouped="true"`) klusteripotsikoille (Nyt / Tänään / Tällä viikolla). `itemTemplate` korttiulkoasulle. |
| Sarakemuotoinen lista (Yleisesittelyt, Tutustumiskäynnit) | **`dx-data-grid`** | ⭐ Käyttäjä jo osaa Column Chooserin ja filtterit. Älä korvaa. |
| Pikahaku / globaali haku | **`dx-text-box`** + **`dx-list`** tuloksille; tai **`dx-drop-down-box`** | Älä rakenna omaa autocompletea |
| Filteripainike | **`dx-button`** + **`dx-popover`** tai `dx-drop-down-button` | |
| Pillit / chipit (filtterit) | **`dx-tag-box`** read-only-tilassa, tai `dx-button-group` valinnoille | |
| Tehtäväkortin avaaminen → detail | **`dx-popup`** modaalille TAI Angular-routeri kokonäkymälle | Mobiilissa: koko ruutu (router). Tabletissa: dx-popup `width="600"`. |
| Päivämäärävalitsija | **`dx-date-box`** (`type="date"` / `"datetime"`) | Lokalisointi `fi-FI` |
| Lomakekentät detail-näkymässä | **`dx-form`** | Käyttää automaattisesti DevExtreme-lomakkeen layouttia |
| Painikkeet (Soita, Avaa, Postpone) | **`dx-button`** | `stylingMode="contained" \| "outlined" \| "text"` — älä kirjoita omaa CSS-tyyppiluokitusta |
| Tilailmaisin (Aktiivinen, Valmis, Peruutettu) | **`dx-tag-box`** read-only tai pelkkä div + `--lumo-status-*` token | DevExtremessä ei suoraa "badge"-komponenttia |
| Bottom-navigaation välilehdet | **`dx-tabs`** `scrollByContent` + custom-css ALAreunaan kiinnitettäväksi | Tai oma komponentti tokeneilla — DevExtremen `dx-tabs` on suunniteltu yläreunaan |
| "Lisää"-välilehden moduulivalikko | **`dx-tree-view`** | XAFin nykyinen vasen valikko on tree-view; käytä samaa |
| Toast / ilmoitus | **`dx-toast`** | "Tehtävä siirretty huomiselle", "Yhteys palautettu" |
| Vahvistusdialogit | **`dx-popup`** + footer-painikkeet, tai **`confirm()`** DevExtreme-utilista | |
| Pull-to-refresh | **`dx-list`** `pullRefreshEnabled="true"` | Pakollinen mobiilissa |
| Loading | **`dx-load-indicator`** / **`dx-load-panel`** | Älä piirrä omaa spinneriä |
| Numero/laskuri (esim. Valmiina-%) | **`dx-number-box`** tai **`dx-progress-bar`** | |

## 3. Tyylittäminen — `lumo-devextreme-overrides.scss`

DevExtremen oletuslook (Generic.light) ei sovi Lumon brändiin. Repossa on jo `design/lumo-devextreme-overrides.scss` — **kaikki brand-poikkeamat menevät sinne**, eivät komponenttikohtaiseen CSS:ään.

Yleinen ohje:

```scss
// design/lumo-devextreme-overrides.scss

@import "lumo-tokens.css";  // CSS-muuttujat saataville

// 1. Päävärit
.dx-button-mode-contained.dx-button-default {
  background-color: var(--lumo-cta);
  color: var(--lumo-cta-on);
  border-radius: var(--lumo-radius-md);

  &:hover { background-color: var(--lumo-cta-hover); }
}

// 2. DataGrid header-kapeneva
.dx-datagrid-headers {
  background-color: var(--lumo-surface-2);
  font-family: var(--lumo-font-heading);
  font-weight: 600;
}

// 3. dx-list jonokorteille
.dx-list-item.lumo-task-card {
  padding: var(--lumo-spacing-md);
  border-left: 3px solid var(--lumo-accent-navy);

  &.lumo-urgent { border-left-color: var(--lumo-cta); }
}

// 4. Bottom-tabs erikoissääntö
.lumo-bottom-tabs.dx-tabs {
  background: var(--lumo-surface-1);
  border-top: 1px solid var(--lumo-border-subtle);
  // ... ks. SCREENS.md §1.3 mitat
}
```

**Säännöt:**

1. **Älä koskaan ohita DevExtremen luokkia inline-tyyleillä** Angular-templatessa — kaikki menee `lumo-devextreme-overrides.scss`:ään
2. **Käytä CSS-muuttujia**, ei kovakoodattuja hex-arvoja
3. **Suosi `stylingMode`-propsia** ennen kuin lisäät custom-CSS:ää (DevExtremen omat muunnelmat usein riittävät)
4. **Älä poista DevExtremen base-tyylejä** (`dx.generic.css`) — ohita ne, älä korvaa

## 4. DataGridin erikoiset ominaisuudet jotka säilytetään

Nykyisestä XAF Blazor -näkymästä (ks. `screenshots/_existing-*.png`) käyttäjä **odottaa nämä toiminnallisuudet**:

| Toiminto | DevExtreme-asetus | Pakollisuus |
|---|---|---|
| Sarakkeenvalitsin (Column Chooser) | `columnChooser.enabled="true"`, `mode="select"` | ⭐ Pakollinen — käyttäjä osaa tämän |
| Sarakekohtaiset filtterit (suppilo) | `filterRow.visible="true"`, `headerFilter.visible="true"` | Pakollinen |
| Sarakkeiden uudelleenjärjestys (drag) | `allowColumnReordering="true"` | Pakollinen |
| Sivutus | `paging.pageSize`, `pager.allowedPageSizes` | Pakollinen, oletus 20 |
| Ryhmittely otsikkoa raahaamalla ("Drag a column header here…") | `groupPanel.visible="true"` | Säilytä Tehtävät-grideissä |
| Vienti (Excel) | `export.enabled="true"` | Pakollinen ASMA-listoissa |
| Master/detail-paneeli | Custom layout `dx-data-grid` + erillinen `dx-form` oikealla | Säilytä työpöydällä; mobiilissa tilalle dx-popup |

## 5. Mobiili vs. työpöytä — mitä eroaa

A+C-suunta on **mobiililähtöinen**, mutta XAF Blazor -versio tehdään työpöydälle. Sama Angular-koodi tarjoilee molemmat — DevExtreme on responsiivinen, mutta vaatii sääntöjä:

| Näkymä | Mobiili (≤ 768px) | Työpöytä (≥ 1024px) |
|---|---|---|
| Tehtäväjono (etusivu) | `dx-list` ryhmitelty | `dx-data-grid` ryhmittelyllä |
| Tehtävän detail | Koko ruutu (router) | `dx-popup` 600px tai master-detail |
| Bottom-tabs | Näkyvissä alalaidassa | Piilotettu — käytä XAF-vasemman valikon | 
| Vasen valikko | Piilotettu, "Lisää"-välilehden takana | Aina näkyvissä |
| Pikahaku | Modaali (koko ruutu) | `dx-popup` 600×400 |

Käytä Angular CDK:n `BreakpointObserver`a tai DevExtremen `dx-responsive-box`ia kytkeäksesi näiden välillä — älä kirjoita kahta erillistä komponenttia.

## 6. Mitä **ei** saa tehdä

- ❌ Käyttää Material-, Bootstrap- tai PrimeNG-komponentteja DevExtremen rinnalla. Saman moduulin sisällä kahta UI-kirjastoa = käyttöliittymäkaaos.
- ❌ Kirjoittaa omaa `<table>`-näkymää kun `dx-data-grid` riittää
- ❌ Tehdä omaa `<input>`-validoitua lomaketta kun `dx-form` + `validation`-rule kelpaa
- ❌ Korvata DevExtremen Column Chooseria omalla "näytä/piilota sarakkeet" -valikolla
- ❌ Piirtää bottom-sheet-modaalia raa'asti — käytä `dx-popup` `position="bottom"` `dragEnabled`

## 7. Esimerkki: tehtäväjono dx-list:lla

```typescript
// task-queue.component.ts
@Component({
  selector: 'lumo-task-queue',
  template: `
    <dx-list
      [dataSource]="tasks$ | async"
      [grouped]="true"
      [collapsibleGroups]="true"
      [pullRefreshEnabled]="true"
      (onPullRefresh)="refresh()"
      (onItemClick)="openTask($event.itemData)"
      class="lumo-task-list">

      <div *dxTemplate="let group of 'group'" class="lumo-group-header">
        {{ group.key | groupLabel }}
        <span class="lumo-group-count">{{ group.items.length }}</span>
      </div>

      <div *dxTemplate="let task of 'item'"
           class="lumo-task-card"
           [class.lumo-urgent]="task.urgent"
           [class.lumo-accent-navy]="task.accent === 'navy'"
           [class.lumo-accent-cta]="task.accent === 'cta'">

        <div class="lumo-task-when">{{ task.when.time }}</div>
        <h3 class="lumo-task-title">{{ task.title }}</h3>
        <div class="lumo-task-who" *ngIf="task.who">{{ task.who }}</div>
        <div class="lumo-task-meta">{{ task.meta }}</div>

        <div class="lumo-task-actions">
          <dx-button *ngFor="let a of task.actions"
                     [text]="a.label"
                     [stylingMode]="a.primary ? 'contained' : 'outlined'"
                     [type]="a.primary ? 'default' : 'normal'"
                     (onClick)="dispatch(a, task)">
          </dx-button>
        </div>
      </div>
    </dx-list>
  `
})
export class TaskQueueComponent { /* ... */ }
```

Huomioi: **kaikki tyylittäminen** menee `.lumo-task-card`, `.lumo-urgent` jne. luokille `lumo-devextreme-overrides.scss`:ssä — ei inline-tyyleinä Angular-templateen.

## 8. DevExtreme-versio

mVasun nykyinen versio: **dx 23.x** (vahvista `package.json`:sta repon puolella).

Angular-versio: **DevExtreme Angular** -paketti `devextreme-angular`. Saman version pitää vastata `devextreme`-paketin versiota.

Lisenssiavain on jo `app.module.ts`:ssä — älä koske siihen.

## 9. Lisätietoja

- DevExtreme Angular dokumentit: https://js.devexpress.com/Angular/Documentation/
- DevExtreme-teemarakentaja (jos pitää generoida uusi base-teema): https://devexpress.github.io/ThemeBuilder/
- mVasu nykytila ruutukaappauksina: `screenshots/_existing-*.png` — sieltä näkee mitä DevExtreme-toiminnallisuuksia käyttäjä jo tuntee
