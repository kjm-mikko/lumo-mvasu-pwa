import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';

import { environment } from '../../../environments/environment';
import type { TiskilistaCardDto } from '../models/tiskilista-card.dto';
import type { TiskilistaDetailDto } from '../models/tiskilista-detail.dto';
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
    if (query.userLat !== null) params = params.set('userLat', String(query.userLat));
    if (query.userLon !== null) params = params.set('userLon', String(query.userLon));

    return this.http.get<TiskilistaPageDto>(`${this.baseUrl}/api/tiskilista`, { params });
  }

  get(id: string): Observable<TiskilistaDetailDto> {
    return this.http.get<TiskilistaDetailDto>(`${this.baseUrl}/api/tiskilista/${id}`);
  }
}

export type { TiskilistaCardDto };
