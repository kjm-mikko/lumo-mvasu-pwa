import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';

import { environment } from '../../../environments/environment';
import type { TiskilistaCardDto } from '../models/tiskilista-card.dto';
import type { TiskilistaDetailDto } from '../models/tiskilista-detail.dto';
import type { TiskilistaDistinctValuesDto } from '../models/tiskilista-distinct-values.dto';
import type { TiskilistaPageDto } from '../models/tiskilista-page.dto';
import type { TiskilistaListQuery } from '../models/tiskilista-list-query.dto';

@Injectable({ providedIn: 'root' })
export class TiskilistaApiService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = environment.apiBaseUrl;

  list(query: TiskilistaListQuery): Observable<TiskilistaPageDto> {
    let params = new HttpParams()
      .set('scope', query.scope)
      .set('sortBy', query.sortBy)
      .set('page', String(query.page))
      .set('pageSize', String(query.pageSize));

    if (query.q) params = params.set('q', query.q);
    if (query.status) params = params.set('status', query.status);
    if (query.lajit.length > 0) params = params.set('laji', query.lajit.join(','));
    if (query.tyypit.length > 0) params = params.set('tyyppi', query.tyypit.join(','));
    if (query.kunnat.length > 0) params = params.set('kunta', query.kunnat.join(','));
    if (query.kaupunginosat.length > 0) params = params.set('kaupunginosa', query.kaupunginosat.join(','));
    if (query.sopimustilat.length > 0) params = params.set('sopimustila', query.sopimustilat.join(','));
    if (query.isannoitsijat.length > 0) params = params.set('isannoitsija', query.isannoitsijat.join(','));
    if (query.markkinoijat.length > 0) params = params.set('markkinoija', query.markkinoijat.join(','));
    if (query.neliotMin !== null) params = params.set('neliotMin', String(query.neliotMin));
    if (query.neliotMax !== null) params = params.set('neliotMax', String(query.neliotMax));
    if (query.vapautuuFrom) params = params.set('vapautuuFrom', query.vapautuuFrom);
    if (query.vapautuuTo) params = params.set('vapautuuTo', query.vapautuuTo);
    if (query.onKuvausTarveOnly) params = params.set('onKuvausTarveOnly', 'true');
    if (query.lumoFiOnly) params = params.set('lumoFiOnly', 'true');
    if (query.hasUpcomingEsittelyOnly) params = params.set('hasUpcomingEsittelyOnly', 'true');
    if (query.userLat !== null) params = params.set('userLat', String(query.userLat));
    if (query.userLon !== null) params = params.set('userLon', String(query.userLon));

    return this.http.get<TiskilistaPageDto>(`${this.baseUrl}/api/tiskilista`, { params });
  }

  get(id: string): Observable<TiskilistaDetailDto> {
    return this.http.get<TiskilistaDetailDto>(`${this.baseUrl}/api/tiskilista/${id}`);
  }

  distinctValues(): Observable<TiskilistaDistinctValuesDto> {
    return this.http.get<TiskilistaDistinctValuesDto>(`${this.baseUrl}/api/tiskilista/distinct-values`);
  }
}

export type { TiskilistaCardDto };
