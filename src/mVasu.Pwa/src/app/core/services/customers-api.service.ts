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
}

function toCustomer(c: CustomerWireDto): Customer {
  const type = c.type as CustomerType;
  switch (type) {
    case 'person':
      return {
        type,
        id: c.id,
        displayName: c.displayName,
        initials: c.initials,
        counts: c.counts,
        primaryAddress: c.primaryAddress ?? undefined,
        city: c.city ?? undefined,
        tag: c.tag ?? undefined,
        phone: c.phone ?? undefined,
        email: c.email ?? undefined,
        firstName: c.firstName ?? '',
        lastName: c.lastName ?? '',
      };
    case 'company':
      return {
        type,
        id: c.id,
        displayName: c.displayName,
        initials: c.initials,
        counts: c.counts,
        primaryAddress: c.primaryAddress ?? undefined,
        city: c.city ?? undefined,
        tag: c.tag ?? undefined,
        phone: c.phone ?? undefined,
        email: c.email ?? undefined,
        companyName: c.companyName ?? c.displayName,
        businessId: c.businessId ?? undefined,
      };
    case 'contact-person':
      return {
        type,
        id: c.id,
        displayName: c.displayName,
        initials: c.initials,
        counts: c.counts,
        primaryAddress: c.primaryAddress ?? undefined,
        city: c.city ?? undefined,
        tag: c.tag ?? undefined,
        phone: c.phone ?? undefined,
        email: c.email ?? undefined,
        firstName: c.firstName ?? '',
        lastName: c.lastName ?? '',
        parentCompanyId: c.parentCompanyId ?? '',
        parentCompanyName: c.parentCompanyName ?? '',
      };
  }
}
