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
import { RouterLink } from '@angular/router';
import { CurrencyPipe, DatePipe, DecimalPipe } from '@angular/common';
import { catchError, debounceTime, distinctUntilChanged, of, switchMap } from 'rxjs';

import { TiskilistaApiService } from '../../core/services/tiskilista-api.service';
import { LocationService } from '../../core/services/location.service';
import type { TiskilistaCardDto } from '../../core/models/tiskilista-card.dto';
import type { TiskilistaPageDto } from '../../core/models/tiskilista-page.dto';
import type {
  TiskilistaListQuery,
  TiskilistaScope,
  TiskilistaSortBy,
} from '../../core/models/tiskilista-list-query.dto';
import { DEFAULT_TISKILISTA_QUERY } from '../../core/models/tiskilista-list-query.dto';

@Component({
  selector: 'app-tiskilista-list',
  imports: [RouterLink, FormsModule, CurrencyPipe, DatePipe, DecimalPipe],
  template: `
    <main class="tiskilista">
      <header>
        <h1>Tiskilista</h1>
        @if (page(); as p) {
          <span class="count">{{ p.total }} huoneistoa</span>
        }
      </header>

      <section class="filters" role="search">
        <input
          class="search"
          type="search"
          placeholder="Hae osoite, kunta tai kaupunginosa…"
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
                <a class="card" [routerLink]="['/tiskilista', item.id]">
                  <div class="card-head">
                    <h2 class="osoite">{{ item.osoite }}</h2>
                    @if (item.distanceKm !== null) {
                      <span class="distance">{{ item.distanceKm | number:'1.0-1' }} km</span>
                    }
                  </div>
                  <div class="card-meta">
                    @if (item.tyyppi) { <span>{{ item.tyyppi }}</span> }
                    @if (item.neliot !== null) { <span>{{ item.neliot | number:'1.0-1' }} m²</span> }
                    @if (item.kerros) { <span>{{ item.kerros }}{{ item.kerroksia ? '/' + item.kerroksia : '' }}. krs</span> }
                  </div>
                  <div class="card-footer">
                    <span class="vuokra">{{ item.vuokra | currency:'EUR':'symbol':'1.0-0' }}</span>
                    @if (item.vapautuu) {
                      <span class="vapautuu">vapautuu {{ item.vapautuu | date:'dd.MM.yyyy' }}</span>
                    }
                  </div>
                  <div class="badges">
                    @if (item.lumoFi) { <span class="badge badge--lumo">Lumo.fi</span> }
                    @if (item.vuokraovi) { <span class="badge">Vuokraovi</span> }
                    @if (item.onKuvausTarve) { <span class="badge badge--warn">Kuvaus tarpeen</span> }
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
  `,
  styleUrl: './tiskilista-list.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class TiskilistaListComponent {
  private readonly api = inject(TiskilistaApiService);
  private readonly location = inject(LocationService);
  private readonly destroyRef = inject(DestroyRef);

  protected readonly searchInput = signal<string>('');
  protected readonly scope = signal<TiskilistaScope>('omat');
  protected readonly status = signal<string | null>(null);
  protected readonly sortBy = signal<TiskilistaSortBy>('vapautuu');
  protected readonly pageNumber = signal<number>(1);

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
      this.sortBy();
      // Skip on the initial run; rely on the query effect to load page 1.
      if (this.pageNumber() !== 1) this.pageNumber.set(1);
    }, { allowSignalWrites: true });

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

    // Eagerly request a position so distance sort becomes available even if
    // the user hasn't visited /settings since loading the app.
    if (this.location.permissionState() === 'granted'
      && this.location.currentPosition() === null) {
      this.location.getCurrent().catch(() => undefined);
    }
  }

  protected goPage(page: number): void {
    if (page < 1) return;
    if (page > this.totalPages()) return;
    this.pageNumber.set(page);
  }
}

export type { TiskilistaCardDto };
