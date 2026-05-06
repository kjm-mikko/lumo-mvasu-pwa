import {
  ChangeDetectionStrategy,
  Component,
  DestroyRef,
  computed,
  inject,
  signal,
} from '@angular/core';
import { Router } from '@angular/router';
import { takeUntilDestroyed, toSignal } from '@angular/core/rxjs-interop';
import {
  BehaviorSubject,
  Subject,
  catchError,
  combineLatest,
  debounceTime,
  distinctUntilChanged,
  map,
  of,
  startWith,
  switchMap,
  tap,
} from 'rxjs';
import { DxButtonModule } from 'devextreme-angular/ui/button';
import { DxListModule } from 'devextreme-angular/ui/list';
import { DxPopupModule } from 'devextreme-angular/ui/popup';
import { DxSelectBoxModule } from 'devextreme-angular/ui/select-box';
import { DxTextBoxModule } from 'devextreme-angular/ui/text-box';
import { DxToastModule } from 'devextreme-angular/ui/toast';

import { CustomersApiService } from '../../core/services/customers-api.service';
import { CustomersService } from '../../core/services/customers.service';
import {
  CUSTOMER_RELATION_LABELS,
  CUSTOMER_TYPE_LABELS,
  totalCount,
  type Customer,
  type CustomerFilters,
  type CustomerRelationFilter,
  type CustomerType,
} from '../../core/models/customer.dto';

type ToastType = 'info' | 'success' | 'warning' | 'error';
interface ToastState {
  readonly visible: boolean;
  readonly message: string;
  readonly type: ToastType;
}
const TOAST_HIDDEN: ToastState = { visible: false, message: '', type: 'info' };

const DEBOUNCE_MS = 200;

const TYPE_OPTIONS: ReadonlyArray<{ readonly id: CustomerType; readonly label: string }> = [
  { id: 'person',          label: CUSTOMER_TYPE_LABELS.person },
  { id: 'company',         label: CUSTOMER_TYPE_LABELS.company },
  { id: 'contact-person',  label: CUSTOMER_TYPE_LABELS['contact-person'] },
];

/**
 * Cities promoted to the filter sheet's quick-pick chip row. Picked
 * by population (Finland's five largest); covers the bulk of Lumo's
 * portfolio with one tap. Cities outside this set are reachable via
 * the dx-select-box typeahead below the chips.
 *
 * Phase 2 follow-up: the backend metadata endpoint can return its
 * own ordered top list (e.g. by customer count), at which point this
 * static array gets dropped.
 */
const TOP_CITIES: ReadonlyArray<string> = [
  'Helsinki', 'Tampere', 'Espoo', 'Vantaa', 'Oulu',
];

const RELATION_OPTIONS: ReadonlyArray<{ readonly id: CustomerRelationFilter; readonly label: string }> = [
  { id: 'has-contract',    label: CUSTOMER_RELATION_LABELS['has-contract'] },
  { id: 'has-application', label: CUSTOMER_RELATION_LABELS['has-application'] },
  { id: 'has-offer',       label: CUSTOMER_RELATION_LABELS['has-offer'] },
  { id: 'has-showing',     label: CUSTOMER_RELATION_LABELS['has-showing'] },
  { id: 'no-relations',    label: CUSTOMER_RELATION_LABELS['no-relations'] },
];

interface CountPill {
  readonly key: string;
  readonly label: string;
  readonly icon: string;
  readonly value: number;
  readonly tone: 'navy' | 'cta' | 'warn' | 'info';
}

/**
 * Asiakkaat tab — long-tail customer browser. SCREENS.md §02 + Lumo
 * domain. Lives at /customers under MainLayout with `data: { reuse:
 * true }` so the search query, applied filters and scroll position
 * survive a detail navigation.
 *
 * The list mixes Henkilo / Yritys / Yhteyshenkilo customers in a
 * single fi-FI-sorted feed; each row's avatar tone, secondary line
 * and count pills are type-aware. Counts surface relations
 * (contracts / applications / offers / showings) so the user can
 * triage at a glance — only non-zero counts render as pills, and an
 * empty-counts row gets a discreet "—" badge for the rare contact-
 * only case.
 */
@Component({
  selector: 'app-customers',
  imports: [
    DxButtonModule,
    DxListModule,
    DxPopupModule,
    DxSelectBoxModule,
    DxTextBoxModule,
    DxToastModule,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <main class="customers">
      <section class="topbar">
        <div class="topbar-text">
          <h1 class="title">Asiakkaat</h1>
          @if (resultCount(); as n) {
            <span class="eyebrow">{{ n }} {{ n === 1 ? 'osuma' : 'osumaa' }}</span>
          } @else {
            <span class="eyebrow">Hae nimellä, osoitteella tai Y-tunnuksella</span>
          }
        </div>
        <dx-button
          class="topbar-search"
          icon="search"
          stylingMode="text"
          [elementAttr]="{ 'aria-label': 'Avaa pikahaku' }"
          (onClick)="openQuickSearch()"
        ></dx-button>
        <dx-button
          class="topbar-filter"
          icon="filter"
          stylingMode="text"
          [elementAttr]="{ 'aria-label': 'Suodattimet' }"
          (onClick)="openFilterSheet()"
        ></dx-button>
      </section>

      <section class="search-bar">
        <dx-text-box
          class="search-input"
          stylingMode="filled"
          placeholder="Hae nimellä, osoitteella, Y-tunnuksella…"
          [value]="query()"
          [valueChangeEvent]="'input'"
          (onValueChanged)="onValue($event)"
          [elementAttr]="{ 'aria-label': 'Hae asiakkaita' }"
        ></dx-text-box>
        @if (loading()) {
          <div class="search-loading" aria-live="polite" aria-busy="true">
            <i class="dx-icon dx-icon-clock" aria-hidden="true"></i>
            <span>Haetaan asiakkaita…</span>
          </div>
        }
      </section>

      @if (filtersActive()) {
        <section class="chip-row" aria-label="Aktiiviset suodattimet">
          @if (filters().type; as type) {
            <button type="button" class="chip"
                    [title]="'Poista suodatin: ' + typeLabel(type)"
                    (click)="clearType()">
              {{ typeLabel(type) }}
              <span class="chip-x" aria-hidden="true">×</span>
            </button>
          }
          @if (filters().relation; as relation) {
            <button type="button" class="chip"
                    [title]="'Poista suodatin: ' + relationLabel(relation)"
                    (click)="clearRelation()">
              {{ relationLabel(relation) }}
              <span class="chip-x" aria-hidden="true">×</span>
            </button>
          }
          @if (filters().city; as city) {
            <button type="button" class="chip"
                    [title]="'Poista suodatin: ' + city"
                    (click)="clearCity()">
              {{ city }}
              <span class="chip-x" aria-hidden="true">×</span>
            </button>
          }
          <button
            type="button"
            class="chip-clear"
            title="Poista kaikki suodattimet"
            (click)="clearAllFilters()"
          >Tyhjennä</button>
        </section>
      }

      @if (loading() && customers().length === 0) {
        <!-- Initial-load skeleton — search-bar shows the inline spinner while
             a list is already on screen, but on first paint we render a light
             list-shaped placeholder so the user sees the request is in flight. -->
        <ul class="skeleton-list" aria-hidden="true">
          @for (_ of [0,1,2,3,4]; track $index) {
            <li class="skeleton-row">
              <span class="skeleton-avatar"></span>
              <span class="skeleton-info">
                <span class="skeleton-line skeleton-line--name"></span>
                <span class="skeleton-line skeleton-line--sub"></span>
              </span>
            </li>
          }
        </ul>
      } @else if (customers().length === 0) {
        <div class="empty">
          <p class="empty-title">Ei osumia</p>
          @if (debouncedQuery().length > 0 || filtersActive()) {
            <p class="empty-sub">Tarkista hakuehdot tai poista suodattimet.</p>
            <dx-button
              class="empty-cta"
              text="Lisää uusi asiakas"
              type="default"
              stylingMode="contained"
              (onClick)="newCustomer()"
            ></dx-button>
          } @else {
            <p class="empty-sub">Aloita kirjoittamalla nimi tai osoite.</p>
          }
        </div>
      } @else {
        <dx-list
          class="lumo-customers-list"
          [dataSource]="customersData()"
          keyExpr="id"
          itemTemplate="row"
          [pageLoadMode]="'scrollBottom'"
          [pageLoadingText]="'Ladataan…'"
          (onItemClick)="onRowClick($event)"
        >
          <div *dxTemplate="let c of 'row'" class="customer-row">
            <span class="avatar" [attr.data-type]="c.type">{{ c.initials }}</span>
            <div class="customer-info">
              <span class="customer-name">
                @for (seg of highlight(c.displayName); track $index) {
                  @if (seg.mark) { <mark>{{ seg.text }}</mark> }
                  @else { {{ seg.text }} }
                }
              </span>
              <span class="customer-secondary">
                @for (seg of highlight(secondaryLine(c)); track $index) {
                  @if (seg.mark) { <mark>{{ seg.text }}</mark> }
                  @else { {{ seg.text }} }
                }
              </span>
              @if (c.tag) {
                <span class="customer-tag" [attr.data-tone]="c.tag.tone">{{ c.tag.label }}</span>
              }
            </div>
            <div class="customer-counts">
              @for (p of pillsFor(c); track p.key) {
                <span class="count-pill" [attr.data-tone]="p.tone" [title]="p.label">
                  <i class="dx-icon dx-icon-{{ p.icon }}" aria-hidden="true"></i>
                  {{ p.value }}
                </span>
              }
              @if (pillsFor(c).length === 0) {
                <span class="count-empty" title="Ei aktiivisia liitoksia">—</span>
              }
            </div>
            <i class="customer-chev dx-icon dx-icon-chevronright" aria-hidden="true"></i>
          </div>
        </dx-list>
      }
    </main>

    <dx-popup
      [visible]="filterSheetVisible()"
      title="Suodattimet"
      [width]="320"
      [height]="'auto'"
      [showCloseButton]="true"
      [hideOnOutsideClick]="true"
      (onHiding)="closeFilterSheet()"
    >
      <div *dxTemplate="let _ of 'content'" class="filter-body">
        <h3 class="filter-h">Asiakastyyppi</h3>
        <div class="filter-options filter-options--wrap">
          <button type="button" class="filter-option"
                  [class.filter-option--active]="!filters().type"
                  (click)="setType(undefined)">Kaikki</button>
          @for (opt of typeOptions; track opt.id) {
            <button type="button" class="filter-option"
                    [class.filter-option--active]="filters().type === opt.id"
                    (click)="setType(opt.id)">{{ opt.label }}</button>
          }
        </div>

        <h3 class="filter-h">Liitokset</h3>
        <div class="filter-options">
          <button type="button" class="filter-option"
                  [class.filter-option--active]="!filters().relation"
                  (click)="setRelation(undefined)">Kaikki</button>
          @for (opt of relationOptions; track opt.id) {
            <button type="button" class="filter-option"
                    [class.filter-option--active]="filters().relation === opt.id"
                    (click)="setRelation(opt.id)">{{ opt.label }}</button>
          }
        </div>

        <h3 class="filter-h">Kunta</h3>
        <div class="filter-options filter-options--wrap">
          <button type="button" class="filter-option"
                  [class.filter-option--active]="!filters().city"
                  (click)="setCity(undefined)">Kaikki</button>
          @for (city of topCitiesAvailable; track city) {
            <button type="button" class="filter-option"
                    [class.filter-option--active]="filters().city === city"
                    (click)="setCity(city)">{{ city }}</button>
          }
        </div>
        <dx-select-box
          class="filter-city-search"
          [items]="citiesData"
          [value]="filters().city ?? null"
          [searchEnabled]="true"
          [showClearButton]="true"
          searchMode="contains"
          placeholder="Hae muista kunnista…"
          (onValueChanged)="onCitySelected($event)"
          [elementAttr]="{ 'aria-label': 'Hae muista kunnista' }"
        ></dx-select-box>
      </div>
    </dx-popup>

    <dx-toast
      [visible]="toast().visible"
      [message]="toast().message"
      [type]="toast().type"
      [displayTime]="2400"
      (onHiding)="onToastHide()"
    ></dx-toast>
  `,
  styleUrl: './customers.component.scss',
})
export class CustomersComponent {
  private readonly customersService = inject(CustomersService);
  private readonly api = inject(CustomersApiService);
  private readonly destroyRef = inject(DestroyRef);
  private readonly router = inject(Router);

  protected readonly typeOptions = TYPE_OPTIONS;
  protected readonly relationOptions = RELATION_OPTIONS;
  protected readonly cities = this.customersService.cities;

  /**
   * Top cities that actually exist in the dataset. Filtering by
   * `cities` here means a TOP_CITIES entry never appears as a chip
   * unless at least one customer is in it — keeps the row lean when
   * the data is sparse (e.g. dev / staging).
   */
  protected readonly topCitiesAvailable: ReadonlyArray<string> =
    TOP_CITIES.filter(c => this.cities.includes(c));

  /** dx-select-box wants a mutable array for `[items]`. */
  protected readonly citiesData: string[] = [...this.cities];

  protected readonly query = signal<string>('');
  protected readonly filters = signal<CustomerFilters>({});
  protected readonly filterSheetVisible = signal<boolean>(false);
  protected readonly toast = signal<ToastState>(TOAST_HIDDEN);

  protected readonly customers = signal<ReadonlyArray<Customer>>([]);
  protected readonly loading = signal<boolean>(false);
  protected readonly loadError = signal<string | null>(null);

  private readonly query$ = new Subject<string>();
  private readonly filters$ = new BehaviorSubject<CustomerFilters>({});

  protected readonly debouncedQuery = toSignal(
    this.query$.pipe(
      startWith(''),
      debounceTime(DEBOUNCE_MS),
      distinctUntilChanged(),
      map(q => q.trim()),
    ),
    { initialValue: '' },
  );

  protected readonly customersData = computed<Customer[]>(() => [...this.customers()]);

  protected readonly resultCount = computed<number | null>(() => {
    const q = this.debouncedQuery();
    return q.length > 0 || this.filtersActive() ? this.customers().length : null;
  });

  protected readonly filtersActive = computed<boolean>(() => {
    const f = this.filters();
    return !!f.type || !!f.relation || !!f.city;
  });

  constructor() {
    // Combined query + filters → debounced HTTP fetch with switchMap so
    // rapid edits cancel the previous request. Errors land in loadError
    // and the customers signal stays as the previous successful page.
    combineLatest([
      this.query$.pipe(startWith('')),
      this.filters$,
    ])
      .pipe(
        debounceTime(DEBOUNCE_MS),
        distinctUntilChanged((a, b) =>
          a[0] === b[0] &&
          a[1].type === b[1].type &&
          a[1].relation === b[1].relation &&
          a[1].city === b[1].city,
        ),
        tap(() => { this.loading.set(true); this.loadError.set(null); }),
        switchMap(([q, f]) =>
          this.api.list(q, f).pipe(
            catchError((err) => {
              console.error('[Customers] /api/customers failed', err);
              this.loadError.set('Asiakkaita ei voitu ladata.');
              return of([] as ReadonlyArray<Customer>);
            }),
          ),
        ),
        takeUntilDestroyed(this.destroyRef),
      )
      .subscribe((list) => {
        this.customers.set(list);
        this.loading.set(false);
      });
  }

  protected onValue(event: { value?: string | null }): void {
    const next = event.value ?? '';
    this.query.set(next);
    this.query$.next(next);
  }

  protected onRowClick(event: { itemData?: Customer }): void {
    const c = event.itemData;
    if (!c) return;
    this.customersService.pushRecent(c.id);
    this.router.navigate(['/customers', c.id]);
  }

  protected openQuickSearch(): void {
    this.router.navigate(['/search']);
  }

  protected openFilterSheet(): void { this.filterSheetVisible.set(true); }
  protected closeFilterSheet(): void { this.filterSheetVisible.set(false); }

  protected setType(type: CustomerType | undefined): void {
    this.updateFilters(f => ({ ...f, type }));
  }
  protected setRelation(relation: CustomerRelationFilter | undefined): void {
    this.updateFilters(f => ({ ...f, relation }));
  }
  protected setCity(city: string | undefined): void {
    this.updateFilters(f => ({ ...f, city }));
  }

  /**
   * dx-select-box clear → null; selection → string. Both flow into the
   * shared filters.city signal so the chip row stays in sync.
   */
  protected onCitySelected(event: { value?: string | null }): void {
    this.setCity(event.value ?? undefined);
  }
  protected clearType(): void     { this.updateFilters(f => ({ ...f, type: undefined })); }
  protected clearRelation(): void { this.updateFilters(f => ({ ...f, relation: undefined })); }
  protected clearCity(): void     { this.updateFilters(f => ({ ...f, city: undefined })); }
  protected clearAllFilters(): void {
    this.filters.set({});
    this.filters$.next({});
  }

  private updateFilters(producer: (current: CustomerFilters) => CustomerFilters): void {
    this.filters.update(producer);
    this.filters$.next(this.filters());
  }

  protected newCustomer(): void {
    this.flash('Uuden asiakkaan lisäys tulossa', 'info');
  }

  protected typeLabel(type: CustomerType): string {
    return CUSTOMER_TYPE_LABELS[type];
  }

  protected relationLabel(relation: CustomerRelationFilter): string {
    return CUSTOMER_RELATION_LABELS[relation];
  }

  /** Per-type secondary line. */
  protected secondaryLine(c: Customer): string {
    if (c.type === 'contact-person') return `Yhteyshenkilö · ${c.parentCompanyName}`;
    if (c.type === 'company') {
      const parts: string[] = [];
      if (c.businessId) parts.push(`Y-tunnus ${c.businessId}`);
      if (c.primaryAddress) parts.push(c.primaryAddress);
      else if (c.city) parts.push(c.city);
      return parts.length > 0 ? parts.join(' · ') : 'Yritys';
    }
    if (c.primaryAddress) return c.primaryAddress;
    if (c.city) return c.city;
    return totalCount(c.counts) === 0 ? 'Ei aktiivisia liitoksia' : '—';
  }

  /**
   * Render only non-zero counts as pills; the order matches the visual
   * weight of the relation (contracts > offers > applications > showings
   * > reservations).
   */
  protected pillsFor(c: Customer): ReadonlyArray<CountPill> {
    const pills: CountPill[] = [];
    if (c.counts.contracts > 0)
      pills.push({ key: 'k', label: 'Sopimukset',         icon: 'home',  value: c.counts.contracts,    tone: 'info' });
    if (c.counts.offers > 0)
      pills.push({ key: 't', label: 'Avoimet tarjoukset', icon: 'edit',  value: c.counts.offers,       tone: 'cta'  });
    if (c.counts.applications > 0)
      pills.push({ key: 'h', label: 'Hakemukset',         icon: 'doc',   value: c.counts.applications, tone: 'navy' });
    if (c.counts.showings > 0)
      pills.push({ key: 'e', label: 'Tulevat esittelyt',  icon: 'event', value: c.counts.showings,     tone: 'navy' });
    if (c.counts.reservations > 0)
      pills.push({ key: 'v', label: 'Sopimusvaraukset',   icon: 'pin',   value: c.counts.reservations, tone: 'info' });
    return pills;
  }

  protected highlight(text: string): ReadonlyArray<{ readonly text: string; readonly mark: boolean }> {
    return this.customersService.highlight(text, this.debouncedQuery());
  }

  protected onToastHide(): void {
    this.toast.set(TOAST_HIDDEN);
  }

  private flash(message: string, type: ToastType): void {
    this.toast.set({ visible: true, message, type });
  }
}
