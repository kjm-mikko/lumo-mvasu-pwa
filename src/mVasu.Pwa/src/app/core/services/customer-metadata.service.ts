import { HttpClient } from '@angular/common/http';
import { Injectable, computed, inject, signal } from '@angular/core';
import { Observable, map, of, shareReplay, tap } from 'rxjs';

import { environment } from '../../../environments/environment';
import type {
  CustomerFieldMetadata,
  CustomerMetadataResponse,
  CustomerTypeMetadata,
} from '../models/customer-metadata.dto';
import type { CustomerType } from '../models/customer.dto';

/**
 * Loads `/api/customers/metadata` once and caches it for the lifetime
 * of the SPA. The metadata is derived from compiled XAF attributes on
 * the backend, so it doesn't change between API requests — a single
 * fetch is enough to power required-markers, read-only styling and
 * appearance-rule lookups in the customer-detail view.
 *
 * Components that need metadata should subscribe to {@link load} once
 * (or use `await firstValueFrom(...)`) and then read field info via
 * {@link fieldFor}. The internal signal lets templates react when the
 * payload finally lands.
 */
@Injectable({ providedIn: 'root' })
export class CustomerMetadataService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = environment.apiBaseUrl;

  /** Latest metadata payload, or null until the first request resolves. */
  readonly metadata = signal<CustomerMetadataResponse | null>(null);

  /** True once a payload has been cached — drives template @if guards. */
  readonly ready = computed(() => this.metadata() !== null);

  private readonly fetch$: Observable<CustomerMetadataResponse> = this.http
    .get<CustomerMetadataResponse>(`${this.baseUrl}/api/customers/metadata`)
    .pipe(
      tap((response) => this.metadata.set(response)),
      shareReplay({ bufferSize: 1, refCount: false }),
    );

  /**
   * Returns a hot observable that resolves to the cached payload (or
   * triggers the underlying HTTP fetch when called the first time).
   * Subsequent callers receive the same cached response.
   */
  load(): Observable<CustomerMetadataResponse> {
    const cached = this.metadata();
    if (cached) return of(cached);
    return this.fetch$;
  }

  /** Convenience getter for a per-type bundle. */
  forType(type: CustomerType): CustomerTypeMetadata | null {
    const m = this.metadata();
    if (!m) return null;
    if (type === 'person')         return m.person;
    if (type === 'company')        return m.company;
    if (type === 'contact-person') return m.contactPerson;
    return null;
  }

  /** Look up a single field's metadata, or undefined if not present. */
  fieldFor(type: CustomerType, name: string): CustomerFieldMetadata | undefined {
    const t = this.forType(type);
    return t?.fields.find((f) => f.name === name);
  }

  /**
   * Returns the SCSS class hint for an appearance rule whose target
   * field matches the wire-side name, or `null` when no rule applies.
   * Per the agreed plan, the rule's criterion is evaluated by the
   * caller via a hardcoded helper (see henkilo-appearance.ts) — this
   * service only resolves the *style* once a Kind is known.
   */
  styleHintForKind(type: CustomerType, kind: string): string | null {
    const t = this.forType(type);
    return t?.appearanceRules.find((r) => r.kind === kind)?.styleHint ?? null;
  }
}

// Re-export for component-side imports — keeps the public surface tidy.
export type { CustomerFieldMetadata, CustomerTypeMetadata } from '../models/customer-metadata.dto';
export { mapByName } from './customer-metadata.helpers';
