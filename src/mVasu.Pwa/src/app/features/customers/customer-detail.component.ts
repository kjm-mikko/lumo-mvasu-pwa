import {
  ChangeDetectionStrategy,
  Component,
  DestroyRef,
  inject,
  input,
  signal,
} from '@angular/core';
import { takeUntilDestroyed, toObservable } from '@angular/core/rxjs-interop';
import { RouterLink } from '@angular/router';
import { catchError, of, switchMap } from 'rxjs';

import { CustomersApiService } from '../../core/services/customers-api.service';
import {
  CUSTOMER_TYPE_LABELS,
  totalCount,
  type Customer,
  type CustomerType,
} from '../../core/models/customer.dto';

interface CountPill {
  readonly key: string;
  readonly label: string;
  readonly icon: string;
  readonly value: number;
  readonly tone: 'navy' | 'cta' | 'warn' | 'info';
}

/**
 * Asiakas-detail (single Henkilo / Yritys / Yhteyshenkilö). Minimal
 * v1: hero with display name, type badge and count pills, plus a
 * contact section with click-to-call and click-to-mail. List actions
 * (open contract, open application) and an action bar are deferred —
 * we'll build them out once the underlying flows exist.
 */
@Component({
  selector: 'app-customer-detail',
  imports: [RouterLink],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <main class="detail">
      <a class="back" routerLink="/customers">← Takaisin asiakkaisiin</a>

      @if (loading()) {
        <p class="status">Ladataan…</p>
      } @else if (loadError()) {
        <p class="error">Tiedon lataus epäonnistui.</p>
      } @else {
        @let c = item();
        @if (c) {
          <header class="hero">
            <span class="avatar" [attr.data-type]="c.type">{{ c.initials }}</span>
            <div class="hero-text">
              <h1 class="display-name">{{ c.displayName }}</h1>
              <p class="hero-meta">
                <span class="type-badge" [attr.data-type]="c.type">{{ typeLabel(c.type) }}</span>
                @if (c.tag) {
                  <span class="tag" [attr.data-tone]="c.tag.tone">{{ c.tag.label }}</span>
                }
              </p>
            </div>
          </header>

          @if (pillsFor(c).length > 0) {
            <section class="counts" aria-label="Aktiiviset liitokset">
              @for (p of pillsFor(c); track p.key) {
                <span class="count-pill" [attr.data-tone]="p.tone" [title]="p.label">
                  <i class="dx-icon dx-icon-{{ p.icon }}" aria-hidden="true"></i>
                  <span class="count-value">{{ p.value }}</span>
                  <span class="count-label">{{ p.label }}</span>
                </span>
              }
            </section>
          } @else {
            <section class="counts counts--empty">
              <span class="count-empty">Ei aktiivisia liitoksia</span>
            </section>
          }

          <section class="card">
            <h2 class="card-title">Yhteystiedot</h2>
            <dl class="kv">
              @if (c.primaryAddress) {
                <dt>Osoite</dt>
                <dd>{{ c.primaryAddress }}</dd>
              }
              @if (c.city) {
                <dt>Kunta</dt>
                <dd>{{ c.city }}</dd>
              }
              @if (c.phone) {
                <dt>Puhelin</dt>
                <dd><a [href]="'tel:' + c.phone">{{ c.phone }}</a></dd>
              }
              @if (c.email) {
                <dt>Sähköposti</dt>
                <dd><a [href]="'mailto:' + c.email">{{ c.email }}</a></dd>
              }
              @if (!c.primaryAddress && !c.city && !c.phone && !c.email) {
                <dt>—</dt>
                <dd>Ei tallennettuja yhteystietoja</dd>
              }
            </dl>
          </section>

          @if (c.type === 'company') {
            <section class="card">
              <h2 class="card-title">Yritys</h2>
              <dl class="kv">
                <dt>Y-tunnus</dt>
                <dd>{{ c.businessId || '—' }}</dd>
              </dl>
            </section>
          } @else if (c.type === 'contact-person') {
            <section class="card">
              <h2 class="card-title">Yritys</h2>
              <dl class="kv">
                <dt>Edustamansa yritys</dt>
                @if (c.parentCompanyId) {
                  <dd>
                    <a [routerLink]="['/customers', c.parentCompanyId]">
                      {{ c.parentCompanyName }}
                    </a>
                  </dd>
                } @else {
                  <dd>{{ c.parentCompanyName || '—' }}</dd>
                }
              </dl>
            </section>
          }
        }
      }
    </main>
  `,
  styleUrl: './customer-detail.component.scss',
})
export class CustomerDetailComponent {
  private readonly api = inject(CustomersApiService);
  private readonly destroyRef = inject(DestroyRef);

  /** Bound from the route parameter via withComponentInputBinding. */
  readonly id = input.required<string>();

  protected readonly item = signal<Customer | null>(null);
  protected readonly loading = signal<boolean>(true);
  protected readonly loadError = signal<boolean>(false);

  constructor() {
    toObservable(this.id)
      .pipe(
        switchMap((id) => {
          this.loading.set(true);
          this.loadError.set(false);
          this.item.set(null);
          return this.api.get(id).pipe(
            catchError((err) => {
              console.error('[Customers] detail load failed', err);
              this.loadError.set(true);
              return of(null);
            }),
          );
        }),
        takeUntilDestroyed(this.destroyRef),
      )
      .subscribe((c) => {
        this.item.set(c);
        this.loading.set(false);
      });
  }

  protected typeLabel(type: CustomerType): string {
    return CUSTOMER_TYPE_LABELS[type];
  }

  protected pillsFor(c: Customer): ReadonlyArray<CountPill> {
    const pills: CountPill[] = [];
    if (c.counts.contracts > 0)
      pills.push({ key: 'k', label: 'Sopimusta',         icon: 'home',  value: c.counts.contracts,    tone: 'info' });
    if (c.counts.offers > 0)
      pills.push({ key: 't', label: 'Avointa tarjousta', icon: 'edit',  value: c.counts.offers,       tone: 'cta'  });
    if (c.counts.applications > 0)
      pills.push({ key: 'h', label: 'Hakemusta',         icon: 'doc',   value: c.counts.applications, tone: 'navy' });
    if (c.counts.showings > 0)
      pills.push({ key: 'e', label: 'Tulevaa esittelyä', icon: 'event', value: c.counts.showings,     tone: 'navy' });
    if (c.counts.reservations > 0)
      pills.push({ key: 'v', label: 'Sopimusvarausta',   icon: 'pin',   value: c.counts.reservations, tone: 'info' });
    return pills;
  }

  // Kept for parity with list component's "no relations" rendering;
  // the template already special-cases the empty-counts state, so this
  // helper is retained for readability/extension.
  protected hasAnyRelation(c: Customer): boolean {
    return totalCount(c.counts) > 0;
  }
}
