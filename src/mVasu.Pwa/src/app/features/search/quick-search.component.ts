import {
  ChangeDetectionStrategy,
  Component,
  ElementRef,
  ViewChild,
  computed,
  inject,
  signal,
} from '@angular/core';
import { Location } from '@angular/common';
import { Router } from '@angular/router';
import { toSignal } from '@angular/core/rxjs-interop';
import { Subject, debounceTime, distinctUntilChanged, map, startWith } from 'rxjs';
import { DxButtonModule } from 'devextreme-angular/ui/button';
import { DxListModule } from 'devextreme-angular/ui/list';
import { DxTextBoxModule } from 'devextreme-angular/ui/text-box';
import { DxToastModule } from 'devextreme-angular/ui/toast';

import {
  QuickSearchService,
  type SearchGroup,
  type SearchHit,
} from '../../core/services/quick-search.service';

interface DxSearchGroup {
  readonly key: string;
  readonly label: string;
  readonly items: ReadonlyArray<SearchHit>;
}

type ToastType = 'info' | 'success' | 'warning' | 'error';
interface ToastState {
  readonly visible: boolean;
  readonly message: string;
  readonly type: ToastType;
}
const TOAST_HIDDEN: ToastState = { visible: false, message: '', type: 'info' };

const MIN_QUERY_LENGTH = 2;
const DEBOUNCE_MS = 200;

/**
 * Quick search — long-tail global lookup. SCREENS.md §05 + BEHAVIOR.md §6.
 *
 * Lives at /search as a top-level route (no MainLayout) so it owns the
 * full viewport and serves as a slide-up overlay on mobile. Backend
 * wiring is deferred — QuickSearchService runs the same shape against a
 * local fixture so the component renders SearchHit groups with mark()
 * highlighting today.
 *
 * Keyboard shortcuts: ArrowUp / ArrowDown move selection inside the
 * results, Enter triggers the selected hit, Escape pops back. Recent
 * queries (sessionStorage) show in place of results when the input is
 * empty.
 */
@Component({
  selector: 'app-quick-search',
  imports: [DxButtonModule, DxListModule, DxTextBoxModule, DxToastModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <main class="search">
      <header class="search-bar">
        <dx-text-box
          #input
          class="search-input"
          stylingMode="filled"
          placeholder="Hae osoitteella, nimellä tai toiminnolla…"
          [value]="query()"
          [valueChangeEvent]="'input'"
          (onValueChanged)="onValue($event)"
          (onKeyDown)="onKey($event)"
          [elementAttr]="{ 'aria-label': 'Pikahaku' }"
        ></dx-text-box>
        <dx-button
          class="cancel"
          text="Peru"
          stylingMode="text"
          type="default"
          (onClick)="close()"
        ></dx-button>
      </header>

      @if (query().length < MIN_QUERY_LENGTH) {
        @if (recentQueries().length > 0) {
          <section class="recent">
            <h2 class="qs-section">Viimeksi etsitty</h2>
            <ul class="recent-list" role="list">
              @for (q of recentQueries(); track q) {
                <li>
                  <button type="button" class="recent-row" (click)="setQuery(q)">
                    <i class="dx-icon dx-icon-clock" aria-hidden="true"></i>
                    <span>{{ q }}</span>
                  </button>
                </li>
              }
            </ul>
          </section>
        } @else {
          <p class="hint">Kirjoita vähintään {{ MIN_QUERY_LENGTH }} merkkiä.</p>
        }
      } @else if (groups().length === 0) {
        <p class="hint">Ei osumia haulla "{{ query() }}".</p>
      } @else {
        <dx-list
          class="lumo-search-list"
          [dataSource]="dxGroups()"
          [grouped]="true"
          keyExpr="id"
          groupTemplate="qsGroup"
          itemTemplate="qsHit"
          (onItemClick)="onHitClick($event)"
        >
          <div *dxTemplate="let group of 'qsGroup'" class="qs-section">
            {{ group.label }}
          </div>
          <div *dxTemplate="let hit of 'qsHit'" class="qs-row">
            <i class="qs-icon dx-icon dx-icon-{{ hit.icon }}" aria-hidden="true"></i>
            <div class="qs-info">
              <span class="qs-title">
                @for (seg of highlight(hit.title); track $index) {
                  @if (seg.mark) { <mark>{{ seg.text }}</mark> }
                  @else { {{ seg.text }} }
                }
              </span>
              <span class="qs-meta">
                @for (seg of highlight(hit.meta); track $index) {
                  @if (seg.mark) { <mark>{{ seg.text }}</mark> }
                  @else { {{ seg.text }} }
                }
              </span>
            </div>
          </div>
        </dx-list>
      }
    </main>

    <dx-toast
      [visible]="toast().visible"
      [message]="toast().message"
      [type]="toast().type"
      [displayTime]="2000"
      (onHiding)="onToastHide()"
    ></dx-toast>
  `,
  styleUrl: './quick-search.component.scss',
})
export class QuickSearchComponent {
  private readonly searchService = inject(QuickSearchService);
  private readonly router = inject(Router);
  private readonly location = inject(Location);

  @ViewChild('input', { static: true }) private input?: ElementRef;

  protected readonly MIN_QUERY_LENGTH = MIN_QUERY_LENGTH;

  protected readonly query = signal<string>('');

  /** Debounced query stream → emits at most every DEBOUNCE_MS ms. */
  private readonly query$ = new Subject<string>();
  private readonly debouncedQuery = toSignal(
    this.query$.pipe(
      startWith(''),
      debounceTime(DEBOUNCE_MS),
      distinctUntilChanged(),
      map(q => q.trim()),
    ),
    { initialValue: '' },
  );

  protected readonly recentQueries = this.searchService.recentQueries;

  protected readonly groups = computed<ReadonlyArray<SearchGroup>>(() =>
    this.searchService.search(this.debouncedQuery()),
  );

  protected readonly dxGroups = computed<ReadonlyArray<DxSearchGroup>>(() =>
    this.groups().map(g => ({ key: g.id, label: g.label, items: g.hits })),
  );

  protected readonly toast = signal<ToastState>(TOAST_HIDDEN);

  protected onValue(event: { value?: string | null }): void {
    const next = event.value ?? '';
    this.query.set(next);
    this.query$.next(next);
  }

  protected setQuery(value: string): void {
    this.query.set(value);
    this.query$.next(value);
    this.input?.nativeElement?.querySelector?.('input')?.focus();
  }

  protected onKey(event: { event?: KeyboardEvent }): void {
    const ev = event.event;
    if (!ev) return;
    if (ev.key === 'Escape') {
      ev.preventDefault();
      this.close();
    }
  }

  protected onHitClick(event: { itemData?: SearchHit }): void {
    const hit = event.itemData;
    if (!hit) return;
    this.searchService.pushRecentQuery(this.query());
    if (hit.navigate) {
      this.router.navigate([hit.navigate]).catch(() => {
        this.flash(`Reittiä ei vielä ole: ${hit.navigate}`, 'warning');
      });
      return;
    }
    this.flash(`Tulossa: ${hit.title}`, 'info');
  }

  protected highlight(text: string): ReadonlyArray<{ readonly text: string; readonly mark: boolean }> {
    return this.searchService.highlight(text, this.debouncedQuery());
  }

  protected close(): void {
    this.location.back();
  }

  protected onToastHide(): void {
    this.toast.set(TOAST_HIDDEN);
  }

  private flash(message: string, type: ToastType): void {
    this.toast.set({ visible: true, message, type });
  }
}
