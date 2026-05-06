import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, map } from 'rxjs';

import { environment } from '../../../environments/environment';
import type {
  Customer,
  CustomerCounts,
  CustomerFilters,
  CustomerTag,
  CustomerType,
} from '../models/customer.dto';

/** Wire shape returned by `GET /api/customers`. Mirrors mVasu.Api.Contracts.CustomersResponseDto. */
interface CustomersResponseWireDto {
  readonly items: ReadonlyArray<CustomerWireDto>;
  readonly total: number;
}

/**
 * Flat wire shape — the discriminator lives in `type` and per-type
 * fields are nullable. The frontend maps this to its discriminated-
 * union Customer type below.
 */
interface CustomerWireDto {
  readonly id: string;
  readonly type: string;
  readonly displayName: string;
  readonly initials: string;
  readonly counts: CustomerCounts;
  readonly primaryAddress?: string | null;
  readonly city?: string | null;
  readonly tag?: CustomerTag | null;
  readonly phone?: string | null;
  readonly email?: string | null;
  readonly firstName?: string | null;
  readonly lastName?: string | null;
  readonly companyName?: string | null;
  readonly businessId?: string | null;
  readonly parentCompanyId?: string | null;
  readonly parentCompanyName?: string | null;
  // Detail-only canonical fields (list endpoint sends nulls)
  readonly postalCode?: string | null;
  readonly country?: string | null;
  readonly language?: string | null;
  readonly profession?: string | null;
  readonly industry?: string | null;
  readonly workplace?: string | null;
  readonly income?: number | null;
  readonly emailMarketingAllowed?: boolean | null;
  readonly phoneMarketingAllowed?: boolean | null;
  readonly directMarketingForbidden?: boolean | null;
}

@Injectable({ providedIn: 'root' })
export class CustomersApiService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = environment.apiBaseUrl;

  /**
   * GET /api/customers. Server-side filtering by type + relation +
   * city + free-text search; sort is fi-FI by displayName. Returns
   * the full result list — Phase 2 will add pagination.
   */
  list(query: string, filters: CustomerFilters): Observable<ReadonlyArray<Customer>> {
    let params = new HttpParams();
    if (query.trim().length > 0) params = params.set('q', query.trim());
    if (filters.type)            params = params.set('type', filters.type);
    if (filters.relation)        params = params.set('relation', filters.relation);
    if (filters.city)            params = params.set('city', filters.city);

    return this.http
      .get<CustomersResponseWireDto>(`${this.baseUrl}/api/customers`, { params })
      .pipe(map(response => response.items.map(toCustomer)));
  }

  /**
   * GET /api/customers/{id}. Returns the same wire shape as a list row;
   * the detail view today shows the same fields. The id is the
   * AsiakasNumero (int) — passed as a string here because that's how
   * the list row carries it; the backend route constraint parses it
   * back to int.
   */
  get(id: string): Observable<Customer> {
    return this.http
      .get<CustomerWireDto>(`${this.baseUrl}/api/customers/${encodeURIComponent(id)}`)
      .pipe(map(toCustomer));
  }
}

function toCustomer(c: CustomerWireDto): Customer {
  const type = c.type as CustomerType;
  // Shared base — list rows leave the canonical detail fields null, so
  // these spreads contribute nothing on the list path but populate the
  // detail view in full when GetById fills them.
  const base = {
    id: c.id,
    displayName: c.displayName,
    initials: c.initials,
    counts: c.counts,
    primaryAddress: c.primaryAddress ?? undefined,
    city: c.city ?? undefined,
    tag: c.tag ?? undefined,
    phone: c.phone ?? undefined,
    email: c.email ?? undefined,
    postalCode: c.postalCode ?? undefined,
    country: c.country ?? undefined,
    language: c.language ?? undefined,
    profession: c.profession ?? undefined,
    industry: c.industry ?? undefined,
    workplace: c.workplace ?? undefined,
    income: c.income ?? undefined,
    emailMarketingAllowed: c.emailMarketingAllowed ?? undefined,
    phoneMarketingAllowed: c.phoneMarketingAllowed ?? undefined,
    directMarketingForbidden: c.directMarketingForbidden ?? undefined,
  };
  switch (type) {
    case 'person':
      return {
        ...base,
        type,
        firstName: c.firstName ?? '',
        lastName: c.lastName ?? '',
      };
    case 'company':
      return {
        ...base,
        type,
        companyName: c.companyName ?? c.displayName,
        businessId: c.businessId ?? undefined,
      };
    case 'contact-person':
      return {
        ...base,
        type,
        firstName: c.firstName ?? '',
        lastName: c.lastName ?? '',
        parentCompanyId: c.parentCompanyId ?? '',
        parentCompanyName: c.parentCompanyName ?? '',
      };
  }
}
