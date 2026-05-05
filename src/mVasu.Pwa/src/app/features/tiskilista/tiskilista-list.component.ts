import {
  ChangeDetectionStrategy,
  Component,
  DestroyRef,
  computed,
  effect,
  inject,
  signal,
} from '@angular/core';
import { takeUntilDestroyed, toObservable } from '@angular/core/rxjs-interop';
import { FormsModule } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { CurrencyPipe, DatePipe, DecimalPipe } from '@angular/common';
import { catchError, debounceTime, distinctUntilChanged, of, switchMap } from 'rxjs';
import { DxButtonModule } from 'devextreme-angular/ui/button';
import { DxDateBoxModule } from 'devextreme-angular/ui/date-box';
import { DxNumberBoxModule } from 'devextreme-angular/ui/number-box';
import { DxTagBoxModule } from 'devextreme-angular/ui/tag-box';
import { DxToastModule } from 'devextreme-angular/ui/toast';

import { TiskilistaApiService } from '../../core/services/tiskilista-api.service';
import { LocationService } from '../../core/services/location.service';
import type { TiskilistaCardDto } from '../../core/models/tiskilista-card.dto';
import type { TiskilistaPageDto } from '../../core/models/tiskilista-page.dto';
import type { TiskilistaDistinctValuesDto } from '../../core/models/tiskilista-distinct-values.dto';
import { EMPTY_TISKILISTA_DISTINCT_VALUES } from '../../core/models/tiskilista-distinct-values.dto';
import type {
  TiskilistaListQuery,
  TiskilistaScope,
  TiskilistaSortBy,
} from '../../core/models/tiskilista-list-query.dto';
import {
  DEFAULT_TISKILISTA_QUERY,
  TISKILISTA_LAJI_OPTIONS,
} from '../../core/models/tiskilista-list-query.dto';

type MultiSelectKey = 'lajit' | 'tyypit' | 'kunnat' | 'kaupunginosat' | 'sopimustilat' | 'isannoitsijat' | 'markkinoijat';
type BoolFilterKey = 'onKuvausTarveOnly' | 'lumoFiOnly';

type ToastType = 'info' | 'success' | 'warning' | 'error';
interface ToastState {
  readonly visible: boolean;
  readonly message: string;
  readonly type: ToastType;
}
const TOAST_HIDDEN: ToastState = { visible: false, message: '', type: 'info' };

/** Returns true when both Date instances represent the same calendar day. */
function sameDay(a: Date | null, b: Date | null): boolean {
  if (a === null && b === null) return true;
  if (a === null || b === null) return false;
  return a.getFullYear() === b.getFullYear()
    && a.getMonth() === b.getMonth()
    && a.getDate() === b.getDate();
}

/** Format a Date as ISO yyyy-MM-dd in local time (server expects DateOnly). */
function toIsoDate(d: Date | null): string | null {
  if (d === null) return null;
  const year = d.getFullYear();
  const month = String(d.getMonth() + 1).padStart(2, '0');
  const day = String(d.getDate()).padStart(2, '0');
  return `${year}-${month}-${day}`;
}

@Component({
  selector: 'app-tiskilista-list',
  imports: [
    DxButtonModule, DxDateBoxModule, DxNumberBoxModule, DxTagBoxModule, DxToastModule,
    RouterLink, FormsModule, CurrencyPipe, DatePipe, DecimalPipe,
  ],
  template: `
    <main class="tiskilista">
      <header>
        <div class="header-text">
          <h1>Tiskilista</h1>
          @if (page(); as p) {
            <span class="count">{{ p.total }} huoneistoa</span>
          }
        </div>
        <div class="header-actions">
          <dx-button
            icon="refresh"
            stylingMode="text"
            [disabled]="loading()"
            [elementAttr]="{ 'aria-label': 'Päivitä lista' }"
            (onClick)="refreshList()"
          ></dx-button>
          <dx-button
            icon="datafield"
            stylingMode="text"
            [elementAttr]="{ 'aria-label': 'Laske tiskilista' }"
            (onClick)="laskeTiskilista()"
          ></dx-button>
          <dx-button
            icon="search"
            stylingMode="text"
            [elementAttr]="{ 'aria-label': 'Avaa pikahaku' }"
            (onClick)="openQuickSearch()"
          ></dx-button>
        </div>
      </header>

      <section class="filters" role="search">
        <input
          class="search"
          type="search"
          placeholder="Hae osoite, kunta, KP- tai huonetunnus…"
          [ngModel]="searchInput()"
          (ngModelChange)="searchInput.set($event)"
          aria-label="Hae"
        />

        <div class="chips" role="radiogroup" aria-label="Näytä">
          @for (option of scopeOptions; track option.value) {
            <button
              type="button"
              role="radio"
              [attr.aria-checked]="scope() === option.value"
              [class.active]="scope() === option.value"
              (click)="scope.set(option.value)"
            >{{ option.label }}</button>
          }
        </div>

        <div class="chips" role="radiogroup" aria-label="Tila">
          @for (option of statusOptions; track option.value) {
            <button
              type="button"
              role="radio"
              [attr.aria-checked]="status() === option.value"
              [class.active]="status() === option.value"
              (click)="status.set(option.value)"
            >{{ option.label }}</button>
          }
        </div>

        <div class="filter-grid">
          <dx-tag-box
            [items]="distinctValues().lajit.length ? distinctLajiData() : lajiFallback"
            [value]="lajitMutable()"
            [searchEnabled]="true"
            [showSelectionControls]="true"
            [showClearButton]="true"
            [acceptCustomValue]="false"
            stylingMode="outlined"
            placeholder="Kaikki lajit"
            [elementAttr]="{ 'aria-label': 'Laji' }"
            (onValueChanged)="onMultiSelectChanged('lajit', $event)"
          ></dx-tag-box>

          <dx-tag-box
            [items]="distinctTyyppiData()"
            [value]="tyypitMutable()"
            [searchEnabled]="true"
            [showSelectionControls]="true"
            [showClearButton]="true"
            [acceptCustomValue]="false"
            stylingMode="outlined"
            placeholder="Kaikki tyypit"
            [elementAttr]="{ 'aria-label': 'Tyyppi' }"
            (onValueChanged)="onMultiSelectChanged('tyypit', $event)"
          ></dx-tag-box>

          <dx-tag-box
            [items]="distinctKuntaData()"
            [value]="kunnatMutable()"
            [searchEnabled]="true"
            [showSelectionControls]="true"
            [showClearButton]="true"
            [acceptCustomValue]="false"
            stylingMode="outlined"
            placeholder="Kaikki kunnat"
            [elementAttr]="{ 'aria-label': 'Kunta' }"
            (onValueChanged)="onMultiSelectChanged('kunnat', $event)"
          ></dx-tag-box>

          <dx-tag-box
            [items]="kaupunginosatVisible()"
            [value]="kaupunginosatMutable()"
            [searchEnabled]="true"
            [showSelectionControls]="true"
            [showClearButton]="true"
            [acceptCustomValue]="false"
            stylingMode="outlined"
            placeholder="Kaikki kaupunginosat"
            [elementAttr]="{ 'aria-label': 'Kaupunginosa' }"
            (onValueChanged)="onMultiSelectChanged('kaupunginosat', $event)"
          ></dx-tag-box>

          <dx-tag-box
            [items]="distinctSopimustilaData()"
            [value]="sopimustilatMutable()"
            [searchEnabled]="true"
            [showSelectionControls]="true"
            [showClearButton]="true"
            [acceptCustomValue]="false"
            stylingMode="outlined"
            placeholder="Kaikki sopimustilat"
            [elementAttr]="{ 'aria-label': 'Sopimustila' }"
            (onValueChanged)="onMultiSelectChanged('sopimustilat', $event)"
          ></dx-tag-box>

          <dx-tag-box
            [items]="distinctIsannoitsijaData()"
            [value]="isannoitsijatMutable()"
            [searchEnabled]="true"
            [showSelectionControls]="true"
            [showClearButton]="true"
            [acceptCustomValue]="false"
            stylingMode="outlined"
            placeholder="Kaikki isännöitsijät"
            [elementAttr]="{ 'aria-label': 'Isännöitsijä' }"
            (onValueChanged)="onMultiSelectChanged('isannoitsijat', $event)"
          ></dx-tag-box>

          <dx-tag-box
            [items]="distinctMarkkinoijaData()"
            [value]="markkinoijatMutable()"
            [searchEnabled]="true"
            [showSelectionControls]="true"
            [showClearButton]="true"
            [acceptCustomValue]="false"
            stylingMode="outlined"
            placeholder="Kaikki markkinoijat"
            [elementAttr]="{ 'aria-label': 'Markkinoija' }"
            (onValueChanged)="onMultiSelectChanged('markkinoijat', $event)"
          ></dx-tag-box>
        </div>

        <div class="range-grid">
          <div class="range-pair" role="group" aria-label="Pinta-ala (m²)">
            <span class="range-label">Pinta-ala m²</span>
            <dx-number-box
              [value]="$any(neliotMin())"
              [showClearButton]="true"
              [min]="0"
              [step]="5"
              stylingMode="outlined"
              placeholder="min"
              (onValueChanged)="onNumberRangeChanged('neliotMin', $event)"
            ></dx-number-box>
            <span class="range-sep">–</span>
            <dx-number-box
              [value]="$any(neliotMax())"
              [showClearButton]="true"
              [min]="0"
              [step]="5"
              stylingMode="outlined"
              placeholder="max"
              (onValueChanged)="onNumberRangeChanged('neliotMax', $event)"
            ></dx-number-box>
          </div>

          <div class="range-pair" role="group" aria-label="Vapautuu">
            <span class="range-label">Vapautuu</span>
            <dx-date-box
              [value]="$any(vapautuuFrom())"
              [showClearButton]="true"
              displayFormat="dd.MM.yyyy"
              type="date"
              stylingMode="outlined"
              placeholder="alkaen"
              (onValueChanged)="onDateRangeChanged('vapautuuFrom', $event)"
            ></dx-date-box>
            <span class="range-sep">–</span>
            <dx-date-box
              [value]="$any(vapautuuTo())"
              [showClearButton]="true"
              displayFormat="dd.MM.yyyy"
              type="date"
              stylingMode="outlined"
              placeholder="päättyen"
              (onValueChanged)="onDateRangeChanged('vapautuuTo', $event)"
            ></dx-date-box>
          </div>
        </div>

        <div class="bool-toggles" role="group" aria-label="Pikasuodatukset">
          <button
            type="button"
            [attr.aria-pressed]="onKuvausTarveOnly()"
            [class.active]="onKuvausTarveOnly()"
            (click)="toggleBool('onKuvausTarveOnly')"
          >Vain kuvaustarve</button>
          <button
            type="button"
            [attr.aria-pressed]="lumoFiOnly()"
            [class.active]="lumoFiOnly()"
            (click)="toggleBool('lumoFiOnly')"
          >Vain Lumo.fi</button>
        </div>

        <select
          class="sort"
          [ngModel]="sortBy()"
          (ngModelChange)="sortBy.set($event)"
          aria-label="Järjestys"
        >
          <option value="vapautuu">Vapautuu</option>
          <option value="osoite">Osoite</option>
          <option value="vuokra">Vuokra</option>
          @if (canUseDistance()) {
            <option value="distance">Lähimmät</option>
          }
        </select>
      </section>

      @if (loading()) {
        <p class="status">Ladataan…</p>
      } @else if (loadError()) {
        <p class="error">Listan lataus epäonnistui. Yritä uudestaan.</p>
      } @else {
        @let p = page();
        @if (p && p.items.length === 0) {
          <p class="status">Ei tuloksia.</p>
        } @else if (p) {
          <ul class="cards">
            @for (item of p.items; track item.id) {
              <li>
                <a
                  class="card"
                  [class]="cardCssClasses(item)"
                  [routerLink]="['/tiskilista', item.id]"
                >
                  <div class="card-head">
                    <h2 class="osoite">{{ item.osoite }}</h2>
                    @if (item.distanceKm !== null) {
                      <span class="distance">{{ item.distanceKm | number:'1.0-1' }} km</span>
                    }
                  </div>
                  @if (item.kptunnus || item.huonetunnus) {
                    <div class="card-id">
                      @if (item.kptunnus) { <span>{{ item.kptunnus }}</span> }
                      @if (item.kptunnus && item.huonetunnus) { <span class="card-id-sep">/</span> }
                      @if (item.huonetunnus) { <span>{{ item.huonetunnus }}</span> }
                    </div>
                  }
                  <div class="card-meta">
                    @if (item.tyyppi) { <span>{{ item.tyyppi }}</span> }
                    @if (item.neliot !== null) { <span>{{ item.neliot | number:'1.0-1' }} m²</span> }
                    @if (item.kerros) { <span>{{ item.kerros }}{{ item.kerroksia ? '/' + item.kerroksia : '' }}. krs</span> }
                  </div>
                  @if (item.kunta || item.kaupunginosa || item.isannoitsija || item.markkinoija) {
                    <div class="card-meta card-meta--sub">
                      @if (item.kunta) { <span>{{ item.kunta }}</span> }
                      @if (item.kaupunginosa) { <span>{{ item.kaupunginosa }}</span> }
                      @if (item.isannoitsija) { <span>· {{ item.isannoitsija }}</span> }
                      @if (item.markkinoija) { <span>· {{ item.markkinoija }}</span> }
                    </div>
                  }
                  <div class="card-footer">
                    <span class="vuokra">{{ item.vuokra | currency:'EUR':'symbol':'1.0-0' }}</span>
                    @if (item.vapautuu) {
                      <span class="vapautuu">vapautuu {{ item.vapautuu | date:'dd.MM.yyyy' }}</span>
                    }
                  </div>
                  <div class="badges">
                    @if (item.tila) {
                      <span class="badge badge--tila" [class]="tilaBadgeClass(item.tila)">{{ item.tila }}</span>
                    }
                    @if (item.lumoFi) { <span class="badge badge--lumo">Lumo.fi</span> }
                    @if (item.vuokraovi) { <span class="badge">Vuokraovi</span> }
                    @if (item.onKuvausTarve) { <span class="badge badge--warn">Kuvaustarve</span> }
                    @if (item.prio) { <span class="badge badge--prio">Prio {{ item.prio }}</span> }
                    @if (item.sopimusTila) { <span class="badge">{{ item.sopimusTila }}</span> }
                  </div>
                </a>
              </li>
            }
          </ul>

          @if (totalPages() > 1) {
            <nav class="pager" aria-label="Sivutus">
              <button type="button" [disabled]="p.page <= 1" (click)="goPage(p.page - 1)">‹ Edellinen</button>
              <span>Sivu {{ p.page }} / {{ totalPages() }}</span>
              <button type="button" [disabled]="p.page >= totalPages()" (click)="goPage(p.page + 1)">Seuraava ›</button>
            </nav>
          }
        }
      }
    </main>

    <dx-toast
      [visible]="toast().visible"
      [message]="toast().message"
      [type]="toast().type"
      [displayTime]="2500"
      (onHiding)="onToastHide()"
    ></dx-toast>
  `,
  styleUrl: './tiskilista-list.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class TiskilistaListComponent {
  private readonly api = inject(TiskilistaApiService);
  private readonly location = inject(LocationService);
  private readonly destroyRef = inject(DestroyRef);
  private readonly router = inject(Router);

  protected openQuickSearch(): void {
    this.router.navigate(['/search']);
  }

  private static readonly STORAGE_PREFIX = 'lumo:tiskilista:';

  protected readonly searchInput = signal<string>('');
  protected readonly scope = signal<TiskilistaScope>('omat');
  protected readonly status = signal<string | null>(null);
  protected readonly lajit = signal<ReadonlyArray<string>>(TiskilistaListComponent.loadList('lajit'));
  protected readonly tyypit = signal<ReadonlyArray<string>>(TiskilistaListComponent.loadList('tyypit'));
  protected readonly kunnat = signal<ReadonlyArray<string>>(TiskilistaListComponent.loadList('kunnat'));
  protected readonly kaupunginosat = signal<ReadonlyArray<string>>(TiskilistaListComponent.loadList('kaupunginosat'));
  protected readonly sopimustilat = signal<ReadonlyArray<string>>(TiskilistaListComponent.loadList('sopimustilat'));
  protected readonly isannoitsijat = signal<ReadonlyArray<string>>(TiskilistaListComponent.loadList('isannoitsijat'));
  protected readonly markkinoijat = signal<ReadonlyArray<string>>(TiskilistaListComponent.loadList('markkinoijat'));

  // Boolean filters — only "true" surfaces a constraint; "false" means "no filter".
  protected readonly onKuvausTarveOnly = signal<boolean>(TiskilistaListComponent.loadBool('onKuvausTarveOnly'));
  protected readonly lumoFiOnly = signal<boolean>(TiskilistaListComponent.loadBool('lumoFiOnly'));

  // Range filters — persisted as a single JSON object so we don't pile up
  // five more localStorage keys.
  protected readonly neliotMin = signal<number | null>(TiskilistaListComponent.loadRanges().neliotMin);
  protected readonly neliotMax = signal<number | null>(TiskilistaListComponent.loadRanges().neliotMax);
  protected readonly vapautuuFrom = signal<Date | null>(TiskilistaListComponent.loadDate('vapautuuFrom'));
  protected readonly vapautuuTo = signal<Date | null>(TiskilistaListComponent.loadDate('vapautuuTo'));

  protected readonly sortBy = signal<TiskilistaSortBy>('vapautuu');
  protected readonly pageNumber = signal<number>(1);

  /** Loaded once on init from /api/tiskilista/distinct-values. */
  protected readonly distinctValues = signal<TiskilistaDistinctValuesDto>(EMPTY_TISKILISTA_DISTINCT_VALUES);

  /** Fallback laji list while the distinct-values fetch is in flight. */
  protected readonly lajiFallback: string[] = [...TISKILISTA_LAJI_OPTIONS];

  /** Mutable [items] sources for dx-tag-box (DevExtreme rejects ReadonlyArray). */
  protected readonly distinctLajiData = computed<string[]>(() => [...this.distinctValues().lajit]);
  protected readonly distinctTyyppiData = computed<string[]>(() => [...this.distinctValues().tyypit]);
  protected readonly distinctKuntaData = computed<string[]>(() => [...this.distinctValues().kunnat]);
  protected readonly distinctSopimustilaData = computed<string[]>(() => [...this.distinctValues().sopimustilat]);
  protected readonly distinctIsannoitsijaData = computed<string[]>(() => [...this.distinctValues().isannoitsijat]);
  protected readonly distinctMarkkinoijaData = computed<string[]>(() => [...this.distinctValues().markkinoijat]);

  /** Mutable [value] sources — DX wants a fresh string[] reference per render. */
  protected readonly lajitMutable = computed<string[]>(() => [...this.lajit()]);
  protected readonly tyypitMutable = computed<string[]>(() => [...this.tyypit()]);
  protected readonly kunnatMutable = computed<string[]>(() => [...this.kunnat()]);
  protected readonly kaupunginosatMutable = computed<string[]>(() => [...this.kaupunginosat()]);
  protected readonly sopimustilatMutable = computed<string[]>(() => [...this.sopimustilat()]);
  protected readonly isannoitsijatMutable = computed<string[]>(() => [...this.isannoitsijat()]);
  protected readonly markkinoijatMutable = computed<string[]>(() => [...this.markkinoijat()]);

  /**
   * Kaupunginosa list narrows when one or more kunta is selected — only
   * districts that exist for those municipalities should be offered.
   * Without a fetched mapping we can't actually scope; fall back to the
   * full distinct list for now and treat the cascading filter as a
   * server-side concern (the InOperator on KuntaAlue still respects
   * the selection regardless of which kunta provides the value).
   */
  protected readonly kaupunginosatVisible = computed<string[]>(() => [...this.distinctValues().kaupunginosat]);

  protected onMultiSelectChanged(key: MultiSelectKey, event: { value?: string[] | null }): void {
    const next = event.value ?? [];
    const current = this.signalForKey(key)();
    // DX raises onValueChanged on every reference change; bail out if the
    // selection set hasn't actually changed to avoid resetting the page.
    if (next.length === current.length && next.every((x, i) => current[i] === x)) {
      return;
    }
    this.signalForKey(key).set(next);
    TiskilistaListComponent.persistList(key, next);
  }

  private signalForKey(key: MultiSelectKey) {
    switch (key) {
      case 'lajit': return this.lajit;
      case 'tyypit': return this.tyypit;
      case 'kunnat': return this.kunnat;
      case 'kaupunginosat': return this.kaupunginosat;
      case 'sopimustilat': return this.sopimustilat;
      case 'isannoitsijat': return this.isannoitsijat;
      case 'markkinoijat': return this.markkinoijat;
    }
  }

  // -- Boolean filters -------------------------------------------------------

  protected toggleBool(key: BoolFilterKey): void {
    const sig = key === 'onKuvausTarveOnly' ? this.onKuvausTarveOnly : this.lumoFiOnly;
    const next = !sig();
    sig.set(next);
    TiskilistaListComponent.persistBool(key, next);
  }

  private static loadBool(key: BoolFilterKey): boolean {
    if (typeof localStorage === 'undefined') return false;
    return localStorage.getItem(TiskilistaListComponent.STORAGE_PREFIX + key) === 'true';
  }

  private static persistBool(key: BoolFilterKey, value: boolean): void {
    if (typeof localStorage === 'undefined') return;
    const storageKey = TiskilistaListComponent.STORAGE_PREFIX + key;
    try {
      if (value) {
        localStorage.setItem(storageKey, 'true');
      } else {
        localStorage.removeItem(storageKey);
      }
    } catch { /* quota / private mode */ }
  }

  private static loadList(key: MultiSelectKey): ReadonlyArray<string> {
    if (typeof localStorage === 'undefined') return [];
    try {
      const raw = localStorage.getItem(TiskilistaListComponent.STORAGE_PREFIX + key);
      if (!raw) return [];
      const parsed = JSON.parse(raw);
      return Array.isArray(parsed) ? parsed.filter((x): x is string => typeof x === 'string') : [];
    } catch {
      return [];
    }
  }

  private static persistList(key: MultiSelectKey, value: ReadonlyArray<string>): void {
    if (typeof localStorage === 'undefined') return;
    const storageKey = TiskilistaListComponent.STORAGE_PREFIX + key;
    try {
      if (value.length === 0) {
        localStorage.removeItem(storageKey);
      } else {
        localStorage.setItem(storageKey, JSON.stringify(value));
      }
    } catch {
      // localStorage quota / private mode — silently no-op
    }
  }

  // -- Range filters (numbers + dates) ---------------------------------------

  protected onNumberRangeChanged(
    key: 'neliotMin' | 'neliotMax',
    event: { value?: number | null },
  ): void {
    const next = (event.value ?? null) as number | null;
    const sig = key === 'neliotMin' ? this.neliotMin : this.neliotMax;
    if (sig() === next) return;
    sig.set(next);
    TiskilistaListComponent.persistRanges({
      neliotMin: this.neliotMin(),
      neliotMax: this.neliotMax(),
    });
  }

  protected onDateRangeChanged(
    key: 'vapautuuFrom' | 'vapautuuTo',
    event: { value?: Date | string | null },
  ): void {
    const raw = event.value ?? null;
    const next = raw instanceof Date ? raw : (raw ? new Date(raw) : null);
    const sig = key === 'vapautuuFrom' ? this.vapautuuFrom : this.vapautuuTo;
    if (sameDay(sig(), next)) return;
    sig.set(next);
    TiskilistaListComponent.persistDate(key, next);
  }

  private static loadRanges(): { neliotMin: number | null; neliotMax: number | null } {
    if (typeof localStorage === 'undefined') return { neliotMin: null, neliotMax: null };
    try {
      const raw = localStorage.getItem(TiskilistaListComponent.STORAGE_PREFIX + 'ranges');
      if (!raw) return { neliotMin: null, neliotMax: null };
      const parsed = JSON.parse(raw);
      return {
        neliotMin: typeof parsed?.neliotMin === 'number' ? parsed.neliotMin : null,
        neliotMax: typeof parsed?.neliotMax === 'number' ? parsed.neliotMax : null,
      };
    } catch {
      return { neliotMin: null, neliotMax: null };
    }
  }

  private static persistRanges(value: { neliotMin: number | null; neliotMax: number | null }): void {
    if (typeof localStorage === 'undefined') return;
    const key = TiskilistaListComponent.STORAGE_PREFIX + 'ranges';
    try {
      if (value.neliotMin === null && value.neliotMax === null) {
        localStorage.removeItem(key);
      } else {
        localStorage.setItem(key, JSON.stringify(value));
      }
    } catch { /* quota / private mode */ }
  }

  private static loadDate(key: 'vapautuuFrom' | 'vapautuuTo'): Date | null {
    if (typeof localStorage === 'undefined') return null;
    try {
      const raw = localStorage.getItem(TiskilistaListComponent.STORAGE_PREFIX + key);
      if (!raw) return null;
      const dt = new Date(raw);
      return Number.isNaN(dt.getTime()) ? null : dt;
    } catch {
      return null;
    }
  }

  private static persistDate(key: 'vapautuuFrom' | 'vapautuuTo', value: Date | null): void {
    if (typeof localStorage === 'undefined') return;
    const storageKey = TiskilistaListComponent.STORAGE_PREFIX + key;
    try {
      if (value === null) {
        localStorage.removeItem(storageKey);
      } else {
        localStorage.setItem(storageKey, value.toISOString());
      }
    } catch { /* quota / private mode */ }
  }

  protected readonly page = signal<TiskilistaPageDto | null>(null);
  protected readonly loading = signal<boolean>(false);
  protected readonly loadError = signal<boolean>(false);

  protected readonly totalPages = computed(() => {
    const p = this.page();
    if (!p || p.pageSize <= 0) return 0;
    return Math.ceil(p.total / p.pageSize);
  });

  protected readonly canUseDistance = computed(() =>
    this.location.permissionState() === 'granted' && this.location.currentPosition() !== null);

  protected readonly scopeOptions: ReadonlyArray<{ value: TiskilistaScope; label: string }> = [
    { value: 'omat', label: 'Omat' },
    { value: 'kaikki', label: 'Kaikki' },
  ];

  protected readonly statusOptions: ReadonlyArray<{ value: string | null; label: string }> = [
    { value: null, label: 'Kaikki' },
    { value: 'Vapaa', label: 'Vapaa' },
    { value: 'Varattu', label: 'Varattu' },
  ];

  private readonly query = computed<TiskilistaListQuery>(() => {
    const pos = this.location.currentPosition();
    return {
      ...DEFAULT_TISKILISTA_QUERY,
      q: this.searchInput().trim() || null,
      status: this.status(),
      lajit: this.lajit(),
      tyypit: this.tyypit(),
      kunnat: this.kunnat(),
      kaupunginosat: this.kaupunginosat(),
      sopimustilat: this.sopimustilat(),
      isannoitsijat: this.isannoitsijat(),
      markkinoijat: this.markkinoijat(),
      neliotMin: this.neliotMin(),
      neliotMax: this.neliotMax(),
      vapautuuFrom: toIsoDate(this.vapautuuFrom()),
      vapautuuTo: toIsoDate(this.vapautuuTo()),
      onKuvausTarveOnly: this.onKuvausTarveOnly(),
      lumoFiOnly: this.lumoFiOnly(),
      scope: this.scope(),
      sortBy: this.sortBy(),
      userLat: pos?.coords.latitude ?? null,
      userLon: pos?.coords.longitude ?? null,
      page: this.pageNumber(),
      pageSize: 20,
    };
  });

  constructor() {
    // Reset page to 1 whenever any non-pagination filter changes.
    effect(() => {
      this.searchInput();
      this.scope();
      this.status();
      this.lajit();
      this.tyypit();
      this.kunnat();
      this.kaupunginosat();
      this.sopimustilat();
      this.isannoitsijat();
      this.markkinoijat();
      this.neliotMin();
      this.neliotMax();
      this.vapautuuFrom();
      this.vapautuuTo();
      this.onKuvausTarveOnly();
      this.lumoFiOnly();
      this.sortBy();
      // Skip on the initial run; rely on the query effect to load page 1.
      if (this.pageNumber() !== 1) this.pageNumber.set(1);
    }, { allowSignalWrites: true });

    // Fetch distinct values once on init for the filter dropdowns.
    this.api.distinctValues()
      .pipe(
        catchError((err) => {
          console.error('[Tiskilista] distinct-values failed', err);
          return of(EMPTY_TISKILISTA_DISTINCT_VALUES);
        }),
        takeUntilDestroyed(this.destroyRef),
      )
      .subscribe((dv) => this.distinctValues.set(dv));

    toObservable(this.query)
      .pipe(
        debounceTime(300),
        distinctUntilChanged((a, b) => JSON.stringify(a) === JSON.stringify(b)),
        switchMap((q) => {
          this.loading.set(true);
          this.loadError.set(false);
          return this.api.list(q).pipe(
            catchError((err) => {
              console.error('[Tiskilista] list failed', err);
              this.loadError.set(true);
              return of(null);
            }),
          );
        }),
        takeUntilDestroyed(this.destroyRef),
      )
      .subscribe((p) => {
        this.loading.set(false);
        if (p) this.page.set(p);
      });

    // Eagerly request a position so distance shows up without a detour to
    // /settings. We call getCurrent unless the browser already denied
    // permission or the API isn't supported — in 'prompt' state this
    // triggers the browser prompt right on the Tiskilista screen, and in
    // 'granted' state it just returns the cached fix.
    const state = this.location.permissionState();
    if (state !== 'denied'
      && state !== 'unsupported'
      && this.location.currentPosition() === null) {
      this.location.getCurrent().catch(() => undefined);
    }
  }

  protected goPage(page: number): void {
    if (page < 1) return;
    if (page > this.totalPages()) return;
    this.pageNumber.set(page);
  }

  // -- Toolbar actions -------------------------------------------------------

  protected readonly toast = signal<ToastState>(TOAST_HIDDEN);

  protected refreshList(): void {
    this.loading.set(true);
    this.loadError.set(false);
    this.api.list(this.query())
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (p) => { this.loading.set(false); this.page.set(p); this.flash('Lista päivitetty', 'success'); },
        error: () => { this.loading.set(false); this.loadError.set(true); },
      });
  }

  protected laskeTiskilista(): void {
    // Server-side recalculation of the Tiskilista — XAF action
    // TiskilistaViewController.LaskeTiskilistaAction. Wires to a backend
    // endpoint in a follow-up; today this is a stub that surfaces intent.
    this.flash('Laske tiskilista — toiminnallisuus tulossa', 'info');
  }

  protected onToastHide(): void {
    this.toast.set(TOAST_HIDDEN);
  }

  private flash(message: string, type: ToastState['type']): void {
    this.toast.set({ visible: true, message, type });
  }

  /**
   * Left-stripe colour class derived from `vapautuu`:
   * past → red (overdue, action needed),
   * within 30 days → orange (urgent),
   * within 90 days → yellow (soon),
   * later → green (calm),
   * unset → neutral grey.
   */
  protected cardCssClasses(item: TiskilistaCardDto): string {
    if (!item.vapautuu) return 'card card--vapautuu-none';
    const v = new Date(item.vapautuu).getTime();
    const days = Math.round((v - Date.now()) / (1000 * 60 * 60 * 24));
    if (days < 0) return 'card card--vapautuu-past';
    if (days <= 30) return 'card card--vapautuu-soon';
    if (days <= 90) return 'card card--vapautuu-near';
    return 'card card--vapautuu-future';
  }

  /**
   * Pastel tila-badge variant. Mirrors the XAF `Tila` set
   * (Vapaa / Tarjottu / Esittelyssä / Vapaa (Remontti) / Varattu / …)
   * with neutral fall-through for unrecognised values.
   */
  protected tilaBadgeClass(tila: string): string {
    const normalised = tila.trim().toLowerCase();
    if (normalised.startsWith('vapaa (remontti)')) return 'badge--tila-remontti';
    if (normalised.startsWith('vapaa')) return 'badge--tila-vapaa';
    if (normalised.startsWith('tarjottu')) return 'badge--tila-tarjottu';
    if (normalised.startsWith('esittely')) return 'badge--tila-esittely';
    if (normalised.startsWith('varattu')) return 'badge--tila-varattu';
    if (normalised.startsWith('irtisan')) return 'badge--tila-irtisan';
    return 'badge--tila-default';
  }
}

export type { TiskilistaCardDto };
