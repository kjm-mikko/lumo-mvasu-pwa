import {
  ChangeDetectionStrategy,
  Component,
  DestroyRef,
  computed,
  inject,
  input,
  signal,
} from '@angular/core';
import { takeUntilDestroyed, toObservable } from '@angular/core/rxjs-interop';
import { CurrencyPipe } from '@angular/common';
import { RouterLink } from '@angular/router';
import { catchError, of, switchMap } from 'rxjs';

import { CustomersApiService } from '../../core/services/customers-api.service';
import { CustomerMetadataService } from '../../core/services/customer-metadata.service';
import { mapByName } from '../../core/services/customer-metadata.helpers';
import type { CustomerFieldMetadata } from '../../core/models/customer-metadata.dto';
import {
  CUSTOMER_TYPE_LABELS,
  totalCount,
  type Customer,
  type CustomerType,
} from '../../core/models/customer.dto';
import { isSurnameMissing } from './henkilo-appearance';

interface CountPill {
  readonly key: string;
  readonly label: string;
  readonly icon: string;
  readonly value: number;
  readonly tone: 'navy' | 'cta' | 'warn' | 'info';
}

/**
 * Asiakas-detail (single Henkilo / Yritys / Yhteyshenkilö). Layout
 * mirrors the canonical XAF DetailView captured by the
 * `--xaf-fields xVasu.Data.Asma.Asiakas` reflect dump:
 *
 *   - Hero / type badge / optional tag
 *   - Count pills (active relations)
 *   - Yhteystiedot   (Asiakas base — KatuOsoite, PostiNumero,
 *                     PostiToimiPaikka, Maa, Puhelin/Gsm, Email)
 *   - Yritys/Yhteyshenkilö-spesifi kortti (Y-tunnus / parent yritys)
 *   - Henkilötiedot   (Henkilö-only — Ammatti, ToimiAla, TyoPaikka,
 *                     BruttoTulot)
 *   - Asetukset       (kieli + markkinointi-suostumukset, kaikki
 *                     tyypit)
 *
 * Action bar (PikaVaraus, sendSMS, …) is intentionally still missing —
 * those need backend endpoints. tel:/mailto: links carry the immediate
 * mobile use case until then.
 *
 * PII deliberately left off the wire: PersonID (sotu), DOB, Age.
 *
 * Asiakas.InfoMessage (Henkilo's red-banner appearance rule) is XPO
 * non-persistent so SelectData can't project it. The banner is on
 * hold until we derive it from the related ASMA flags.
 */
@Component({
  selector: 'app-customer-detail',
  imports: [RouterLink, CurrencyPipe],
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
          <header class="hero" [class.appearance-error]="surnameAppearance() === 'appearance-error'">
            <span class="avatar" [attr.data-type]="c.type">{{ c.initials }}</span>
            <div class="hero-text">
              <h1 class="display-name">{{ c.displayName }}</h1>
              <p class="hero-meta">
                <span class="type-badge" [attr.data-type]="c.type">{{ typeLabel(c.type) }}</span>
                @if (c.tag) {
                  <span class="tag" [attr.data-tone]="c.tag.tone">{{ c.tag.label }}</span>
                }
              </p>
              @if (surnameAppearance() === 'appearance-error') {
                <p class="hero-warning">⚠ Sukunimi puuttuu</p>
              }
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
                <dt [class.is-required]="meta('primaryAddress')?.required"
                    [class.is-readonly]="meta('primaryAddress')?.readOnly">Katuosoite</dt>
                <dd [class.is-readonly]="meta('primaryAddress')?.readOnly">{{ c.primaryAddress }}</dd>
              }
              @if (c.postalCode || c.city) {
                <dt [class.is-required]="meta('postalCode')?.required || meta('city')?.required"
                    [class.is-readonly]="meta('postalCode')?.readOnly && meta('city')?.readOnly">Postitoimipaikka</dt>
                <dd>
                  @if (c.postalCode) { {{ formatPostalCode(c.postalCode) }} } {{ c.city || '' }}
                </dd>
              }
              @if (c.country) {
                <dt [class.is-required]="meta('country')?.required"
                    [class.is-readonly]="meta('country')?.readOnly">Maa</dt>
                <dd>{{ c.country }}</dd>
              }
              @if (c.phone) {
                <dt [class.is-required]="meta('phone')?.required"
                    [class.is-readonly]="meta('phone')?.readOnly">Puhelin</dt>
                <dd><a [href]="'tel:' + c.phone">{{ c.phone }}</a></dd>
              }
              @if (c.email) {
                <dt [class.is-required]="meta('email')?.required"
                    [class.is-readonly]="meta('email')?.readOnly">Sähköposti</dt>
                <dd><a [href]="'mailto:' + c.email">{{ c.email }}</a></dd>
              }
              @if (!c.primaryAddress && !c.city && !c.postalCode && !c.country && !c.phone && !c.email) {
                <dt>—</dt>
                <dd>Ei tallennettuja yhteystietoja</dd>
              }
            </dl>
          </section>

          @if (c.type === 'company') {
            <section class="card">
              <h2 class="card-title">Yritys</h2>
              <dl class="kv">
                <dt [class.is-required]="meta('businessId')?.required"
                    [class.is-readonly]="meta('businessId')?.readOnly">Y-tunnus</dt>
                <dd>{{ c.businessId || '—' }}</dd>
                @if (c.industry) {
                  <dt [class.is-required]="meta('industry')?.required"
                      [class.is-readonly]="meta('industry')?.readOnly">Toimiala</dt>
                  <dd>{{ c.industry }}</dd>
                }
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
          } @else if (c.type === 'person' && hasPersonDetails(c)) {
            <section class="card">
              <h2 class="card-title">Henkilötiedot</h2>
              <dl class="kv">
                @if (c.profession) {
                  <dt [class.is-required]="meta('profession')?.required"
                      [class.is-readonly]="meta('profession')?.readOnly">Ammatti</dt>
                  <dd>{{ c.profession }}</dd>
                }
                @if (c.industry) {
                  <dt [class.is-required]="meta('industry')?.required"
                      [class.is-readonly]="meta('industry')?.readOnly">Toimiala</dt>
                  <dd>{{ c.industry }}</dd>
                }
                @if (c.workplace) {
                  <dt [class.is-required]="meta('workplace')?.required"
                      [class.is-readonly]="meta('workplace')?.readOnly">Työpaikka</dt>
                  <dd>{{ c.workplace }}</dd>
                }
                @if (c.income !== undefined && c.income !== null) {
                  <dt [class.is-required]="meta('income')?.required"
                      [class.is-readonly]="meta('income')?.readOnly">Bruttotulot</dt>
                  <dd>{{ c.income | currency:'EUR':'symbol':'1.0-0' }}</dd>
                }
              </dl>
            </section>
          }

          @if (hasSettings(c)) {
            <section class="card">
              <h2 class="card-title">Asetukset</h2>
              <dl class="kv">
                @if (c.language) {
                  <dt>Viestintä-kieli</dt>
                  <dd>{{ languageLabel(c.language) }}</dd>
                }
                @if (c.emailMarketingAllowed !== undefined) {
                  <dt>Sähköpostin käyttö</dt>
                  <dd>{{ c.emailMarketingAllowed ? 'Sallittu' : 'Ei sallittu' }}</dd>
                }
                @if (c.phoneMarketingAllowed !== undefined) {
                  <dt>Puhelinnumeron käyttö</dt>
                  <dd>{{ c.phoneMarketingAllowed ? 'Sallittu' : 'Ei sallittu' }}</dd>
                }
                @if (c.directMarketingForbidden !== undefined) {
                  <dt>Suoramarkkinointi</dt>
                  <dd>{{ c.directMarketingForbidden ? 'Kielletty' : 'Sallittu' }}</dd>
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
  private readonly metadataService = inject(CustomerMetadataService);
  private readonly destroyRef = inject(DestroyRef);

  /** Bound from the route parameter via withComponentInputBinding. */
  readonly id = input.required<string>();

  protected readonly item = signal<Customer | null>(null);
  protected readonly loading = signal<boolean>(true);
  protected readonly loadError = signal<boolean>(false);

  /**
   * Field metadata indexed by wire name for the loaded customer's
   * type. Computed off the metadata service signal so the template
   * picks up the payload as soon as the GET /api/customers/metadata
   * promise resolves.
   */
  protected readonly fieldMeta = computed<Readonly<Record<string, CustomerFieldMetadata>>>(() => {
    const c = this.item();
    if (!c) return {};
    const meta = this.metadataService.metadata();
    if (!meta) return {};
    const t = c.type === 'person'  ? meta.person
            : c.type === 'company' ? meta.company
            :                        meta.contactPerson;
    return mapByName(t.fields);
  });

  /** SCSS class to paint on the surname / company-name dt+dd when the underlying SukuNimi is empty (Asiakas Appearance.RuleRequiredField.Highlight). */
  protected readonly surnameAppearance = computed<string | null>(() => {
    const c = this.item();
    if (!c) return null;
    return isSurnameMissing(c) ? 'appearance-error' : null;
  });

  constructor() {
    // Fire-and-forget metadata load — the response is cached for the
    // lifetime of the SPA. No need to gate the detail render on it;
    // fieldMeta() simply returns {} until the payload lands and the
    // template falls back to plain rendering.
    this.metadataService.load().subscribe({
      error: (err) => console.error('[Customers] metadata load failed', err),
    });

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

  /** Convenience for templates: read a single field's metadata or undefined. */
  protected meta(name: string): CustomerFieldMetadata | undefined {
    return this.fieldMeta()[name];
  }

  /**
   * Render a Finnish postal code per the XAF EditMask `[0-9]{1,5}` —
   * trim, drop anything non-numeric, and zero-pad to 5 digits when the
   * stored value is shorter (some legacy rows are missing leading
   * zeros). Numeric strings ≥ 5 chars pass through untouched; non-
   * numeric or empty strings render as-is.
   */
  protected formatPostalCode(value: string | undefined | null): string {
    if (!value) return '';
    const trimmed = value.trim();
    if (!/^\d+$/.test(trimmed)) return trimmed;
    return trimmed.padStart(5, '0').slice(0, 5);
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

  /** True when at least one person-only employment field is populated. */
  protected hasPersonDetails(c: Customer): boolean {
    return !!c.profession || !!c.industry || !!c.workplace
        || (c.income !== undefined && c.income !== null);
  }

  /** True when any consent / language field is populated. */
  protected hasSettings(c: Customer): boolean {
    return !!c.language
        || c.emailMarketingAllowed !== undefined
        || c.phoneMarketingAllowed !== undefined
        || c.directMarketingForbidden !== undefined;
  }

  /** Human-readable label for the LangCode wire value. */
  protected languageLabel(code: string): string {
    const c = code.toLowerCase();
    if (c === 'fi') return 'Suomi';
    if (c === 'sv') return 'Svenska';
    if (c === 'en') return 'English';
    return code;
  }
}
