import {
  ChangeDetectionStrategy,
  Component,
  computed,
  inject,
  signal,
} from '@angular/core';
import { Router } from '@angular/router';
import { toSignal } from '@angular/core/rxjs-interop';
import { Subject, debounceTime, distinctUntilChanged, map, startWith } from 'rxjs';
import { DxButtonModule } from 'devextreme-angular/ui/button';
import { DxListModule } from 'devextreme-angular/ui/list';
import { DxPopupModule } from 'devextreme-angular/ui/popup';
import { DxTextBoxModule } from 'devextreme-angular/ui/text-box';
import { DxToastModule } from 'devextreme-angular/ui/toast';

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
  imports: [DxButtonModule, DxListModule, DxPopupModule, DxTextBoxModule, DxToastModule],
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

      @if (debouncedQuery().length === 0 && !filtersActive() && recentCustomers().length > 0) {
        <section class="recent">
          <h2 class="qs-section">Viimeksi avatut</h2>
          <dx-list
            class="lumo-customers-list"
            [dataSource]="recentData()"
            keyExpr="id"
            itemTemplate="row"
            (onItemClick)="onRowClick($event)"
          >
            <div *dxTemplate="let c of 'row'" class="customer-row">
              <span class="avatar" [attr.data-type]="c.type">{{ c.initials }}</span>
              <div class="customer-info">
                <span class="customer-name">{{ c.displayName }}</span>
                <span class="customer-secondary">{{ secondaryLine(c) }}</span>
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
        </section>
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
          @for (city of cities; track city) {
            <button type="button" class="filter-option"
                    [class.filter-option--active]="filters().city === city"
                    (click)="setCity(city)">{{ city }}</button>
          }
        </div>
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
  private readonly router = inject(Router);

  protected readonly typeOptions = TYPE_OPTIONS;
  protected readonly relationOptions = RELATION_OPTIONS;
  protected readonly cities = this.customersService.cities;

  protected readonly query = signal<string>('');
  protected readonly filters = signal<CustomerFilters>({});
  protected readonly filterSheetVisible = signal<boolean>(false);
  protected readonly toast = signal<ToastState>(TOAST_HIDDEN);

  private readonly query$ = new Subject<string>();
  protected readonly debouncedQuery = toSignal(
    this.query$.pipe(
      startWith(''),
      debounceTime(DEBOUNCE_MS),
      distinctUntilChanged(),
      map(q => q.trim()),
    ),
    { initialValue: '' },
  );

  protected readonly customers = computed<ReadonlyArray<Customer>>(() =>
    this.customersService.list(this.debouncedQuery(), this.filters()),
  );
  protected readonly customersData = computed<Customer[]>(() => [...this.customers()]);

  protected readonly recentCustomers = computed<ReadonlyArray<Customer>>(() =>
    this.customersService.recentCustomers(),
  );
  protected readonly recentData = computed<Customer[]>(() => [...this.recentCustomers()]);

  protected readonly resultCount = computed<number | null>(() => {
    const q = this.debouncedQuery();
    return q.length > 0 || this.filtersActive() ? this.customers().length : null;
  });

  protected readonly filtersActive = computed<boolean>(() => {
    const f = this.filters();
    return !!f.type || !!f.relation || !!f.city;
  });

  protected onValue(event: { value?: string | null }): void {
    const next = event.value ?? '';
    this.query.set(next);
    this.query$.next(next);
  }

  protected onRowClick(event: { itemData?: Customer }): void {
    const c = event.itemData;
    if (!c) return;
    this.customersService.pushRecent(c.id);
    this.flash(`Asiakkaan tarkka näkymä tulossa: ${c.displayName}`, 'info');
  }

  protected openQuickSearch(): void {
    this.router.navigate(['/search']);
  }

  protected openFilterSheet(): void { this.filterSheetVisible.set(true); }
  protected closeFilterSheet(): void { this.filterSheetVisible.set(false); }

  protected setType(type: CustomerType | undefined): void {
    this.filters.update(f => ({ ...f, type }));
  }
  protected setRelation(relation: CustomerRelationFilter | undefined): void {
    this.filters.update(f => ({ ...f, relation }));
  }
  protected setCity(city: string | undefined): void {
    this.filters.update(f => ({ ...f, city }));
  }
  protected clearType(): void { this.filters.update(f => ({ ...f, type: undefined })); }
  protected clearRelation(): void { this.filters.update(f => ({ ...f, relation: undefined })); }
  protected clearCity(): void { this.filters.update(f => ({ ...f, city: undefined })); }
  protected clearAllFilters(): void { this.filters.set({}); }

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
