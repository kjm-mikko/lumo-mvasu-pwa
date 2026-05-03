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

import { PeopleService } from '../../core/services/people.service';
import {
  PERSON_ROLE_LABELS,
  type Person,
  type PersonFilters,
  type PersonRole,
} from '../../core/models/person.dto';

type ToastType = 'info' | 'success' | 'warning' | 'error';
interface ToastState {
  readonly visible: boolean;
  readonly message: string;
  readonly type: ToastType;
}
const TOAST_HIDDEN: ToastState = { visible: false, message: '', type: 'info' };

const DEBOUNCE_MS = 200;

const ROLE_OPTIONS: ReadonlyArray<{ readonly id: PersonRole; readonly label: string }> = [
  { id: 'tenant',    label: PERSON_ROLE_LABELS.tenant },
  { id: 'applicant', label: PERSON_ROLE_LABELS.applicant },
  { id: 'former',    label: PERSON_ROLE_LABELS.former },
];

/**
 * Asukkaat tab — long-tail people browser. SCREENS.md §02 + BEHAVIOR.md §3.
 *
 * Lives at /people under MainLayout with `data: { reuse: true }` so the
 * search query, applied filters and scroll position survive a detail
 * navigation. Mock data only — PeopleService.list() runs the same
 * shape locally; backend swap is a one-method change.
 *
 * Filter sheet (role + city) opens in a dx-popup positioned at the
 * viewport bottom. Active filters render as chips above the list.
 * Empty query falls back to the 5 most recently opened people from
 * sessionStorage; empty result with active query shows a "Lisää uusi
 * hakija" CTA hint.
 */
@Component({
  selector: 'app-people',
  imports: [DxButtonModule, DxListModule, DxPopupModule, DxTextBoxModule, DxToastModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <main class="people">
      <section class="topbar">
        <div class="topbar-text">
          <h1 class="title">Asukkaat</h1>
          @if (resultCount(); as n) {
            <span class="eyebrow">{{ n }} {{ n === 1 ? 'osuma' : 'osumaa' }}</span>
          } @else {
            <span class="eyebrow">Hae nimellä, osoitteella tai puhelinnumerolla</span>
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
          [icon]="filtersActive() ? 'filter' : 'filter'"
          stylingMode="text"
          [elementAttr]="{ 'aria-label': 'Suodattimet' }"
          (onClick)="openFilterSheet()"
        ></dx-button>
      </section>

      <section class="search-bar">
        <dx-text-box
          class="search-input"
          stylingMode="filled"
          placeholder="Hae nimellä, osoitteella…"
          [value]="query()"
          [valueChangeEvent]="'input'"
          (onValueChanged)="onValue($event)"
          [elementAttr]="{ 'aria-label': 'Hae asukkaita' }"
        ></dx-text-box>
      </section>

      @if (filtersActive()) {
        <section class="chip-row" aria-label="Aktiiviset suodattimet">
          @if (filters().role; as role) {
            <button
              type="button"
              class="chip"
              [title]="'Poista suodatin: ' + roleLabel(role)"
              (click)="clearRole()"
            >
              {{ roleLabel(role) }}
              <span class="chip-x" aria-hidden="true">×</span>
            </button>
          }
          @if (filters().city; as city) {
            <button
              type="button"
              class="chip"
              [title]="'Poista suodatin: ' + city"
              (click)="clearCity()"
            >
              {{ city }}
              <span class="chip-x" aria-hidden="true">×</span>
            </button>
          }
          <button
            type="button"
            class="chip-clear"
            title="Poista kaikki suodattimet"
            (click)="clearAllFilters()"
          >
            Tyhjennä
          </button>
        </section>
      }

      @if (debouncedQuery().length === 0 && !filtersActive() && recentPeople().length > 0) {
        <section class="recent">
          <h2 class="qs-section">Viimeksi avatut</h2>
          <dx-list
            class="lumo-people-list"
            [dataSource]="recentPeopleData()"
            keyExpr="id"
            itemTemplate="row"
            (onItemClick)="onRowClick($event)"
          >
            <div *dxTemplate="let p of 'row'" class="people-row">
              <span class="avatar" [attr.data-tone]="p.tag?.tone ?? 'navy'">{{ p.initials }}</span>
              <div class="people-info">
                <span class="people-name">{{ p.name }}</span>
                <span class="people-meta">{{ p.meta }}</span>
              </div>
              @if (p.tag) {
                <span class="people-tag" [attr.data-tone]="p.tag.tone">{{ p.tag.label }}</span>
              }
              <i class="people-chev dx-icon dx-icon-chevronright" aria-hidden="true"></i>
            </div>
          </dx-list>
        </section>
      } @else if (people().length === 0) {
        <div class="empty">
          <p class="empty-title">Ei osumia</p>
          @if (debouncedQuery().length > 0) {
            <p class="empty-sub">Tarkista hakuehdot tai poista suodattimet.</p>
            <dx-button
              class="empty-cta"
              text="Lisää uusi hakija"
              type="default"
              stylingMode="contained"
              (onClick)="newApplicant()"
            ></dx-button>
          } @else {
            <p class="empty-sub">Aloita kirjoittamalla nimi tai osoite.</p>
          }
        </div>
      } @else {
        <dx-list
          class="lumo-people-list"
          [dataSource]="peopleData()"
          keyExpr="id"
          itemTemplate="row"
          [pageLoadMode]="'scrollBottom'"
          [pageLoadingText]="'Ladataan…'"
          (onItemClick)="onRowClick($event)"
        >
          <div *dxTemplate="let p of 'row'" class="people-row">
            <span class="avatar" [attr.data-tone]="p.tag?.tone ?? 'navy'">{{ p.initials }}</span>
            <div class="people-info">
              <span class="people-name">
                @for (seg of highlight(p.name); track $index) {
                  @if (seg.mark) { <mark>{{ seg.text }}</mark> }
                  @else { {{ seg.text }} }
                }
              </span>
              <span class="people-meta">
                @for (seg of highlight(p.meta); track $index) {
                  @if (seg.mark) { <mark>{{ seg.text }}</mark> }
                  @else { {{ seg.text }} }
                }
              </span>
            </div>
            @if (p.tag) {
              <span class="people-tag" [attr.data-tone]="p.tag.tone">{{ p.tag.label }}</span>
            }
            <i class="people-chev dx-icon dx-icon-chevronright" aria-hidden="true"></i>
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
        <h3 class="filter-h">Rooli</h3>
        <div class="filter-options">
          <button
            type="button"
            class="filter-option"
            [class.filter-option--active]="!filters().role"
            (click)="setRole(undefined)"
          >Kaikki</button>
          @for (opt of roleOptions; track opt.id) {
            <button
              type="button"
              class="filter-option"
              [class.filter-option--active]="filters().role === opt.id"
              (click)="setRole(opt.id)"
            >{{ opt.label }}</button>
          }
        </div>

        <h3 class="filter-h">Kunta</h3>
        <div class="filter-options filter-options--wrap">
          <button
            type="button"
            class="filter-option"
            [class.filter-option--active]="!filters().city"
            (click)="setCity(undefined)"
          >Kaikki</button>
          @for (city of cities; track city) {
            <button
              type="button"
              class="filter-option"
              [class.filter-option--active]="filters().city === city"
              (click)="setCity(city)"
            >{{ city }}</button>
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
  styleUrl: './people.component.scss',
})
export class PeopleComponent {
  private readonly peopleService = inject(PeopleService);
  private readonly router = inject(Router);

  protected readonly roleOptions = ROLE_OPTIONS;
  protected readonly cities = this.peopleService.cities;

  protected readonly query = signal<string>('');
  protected readonly filters = signal<PersonFilters>({});
  protected readonly filterSheetVisible = signal<boolean>(false);
  protected readonly toast = signal<ToastState>(TOAST_HIDDEN);

  /** Debounced query stream → emits at most every DEBOUNCE_MS ms. */
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

  protected readonly people = computed<ReadonlyArray<Person>>(() =>
    this.peopleService.list(this.debouncedQuery(), this.filters()),
  );

  /** dx-list mutates the dataSource array internally; hand it a mutable copy. */
  protected readonly peopleData = computed<Person[]>(() => [...this.people()]);

  protected readonly recentPeople = computed<ReadonlyArray<Person>>(() =>
    this.peopleService.recentPeople(),
  );
  protected readonly recentPeopleData = computed<Person[]>(() => [...this.recentPeople()]);

  protected readonly resultCount = computed<number | null>(() => {
    const q = this.debouncedQuery();
    return q.length > 0 || this.filtersActive() ? this.people().length : null;
  });

  protected readonly filtersActive = computed<boolean>(() => {
    const f = this.filters();
    return !!f.role || !!f.city;
  });

  protected onValue(event: { value?: string | null }): void {
    const next = event.value ?? '';
    this.query.set(next);
    this.query$.next(next);
  }

  protected onRowClick(event: { itemData?: Person }): void {
    const p = event.itemData;
    if (!p) return;
    this.peopleService.pushRecent(p.id);
    this.flash(`Henkilön tarkka näkymä tulossa: ${p.name}`, 'info');
  }

  protected openQuickSearch(): void {
    this.router.navigate(['/search']);
  }

  protected openFilterSheet(): void {
    this.filterSheetVisible.set(true);
  }

  protected closeFilterSheet(): void {
    this.filterSheetVisible.set(false);
  }

  protected setRole(role: PersonRole | undefined): void {
    this.filters.update(f => ({ ...f, role }));
  }

  protected setCity(city: string | undefined): void {
    this.filters.update(f => ({ ...f, city }));
  }

  protected clearRole(): void {
    this.filters.update(f => ({ ...f, role: undefined }));
  }

  protected clearCity(): void {
    this.filters.update(f => ({ ...f, city: undefined }));
  }

  protected clearAllFilters(): void {
    this.filters.set({});
  }

  protected newApplicant(): void {
    this.flash('Hakijan lisäys tulossa', 'info');
  }

  protected roleLabel(role: PersonRole): string {
    return PERSON_ROLE_LABELS[role];
  }

  protected highlight(text: string): ReadonlyArray<{ readonly text: string; readonly mark: boolean }> {
    return this.peopleService.highlight(text, this.debouncedQuery());
  }

  protected onToastHide(): void {
    this.toast.set(TOAST_HIDDEN);
  }

  private flash(message: string, type: ToastType): void {
    this.toast.set({ visible: true, message, type });
  }
}
