import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, map, of } from 'rxjs';

import { environment } from '../../../environments/environment';
import type {
  SearchGroup,
  SearchGroupId,
  SearchHit,
} from './quick-search.service';

/** Wire shape returned by `GET /api/search`. Mirrors mVasu.Api.Contracts.SearchResponseDto. */
interface SearchResponseWireDto {
  readonly groups: ReadonlyArray<SearchGroupWireDto>;
}

interface SearchGroupWireDto {
  readonly id: string;
  readonly label: string;
  readonly hits: ReadonlyArray<SearchHitWireDto>;
}

interface SearchHitWireDto {
  readonly id: string;
  readonly title: string;
  readonly meta: string;
  readonly icon: string;
  readonly navigate?: string | null;
}

const MIN_QUERY_LENGTH = 2;

@Injectable({ providedIn: 'root' })
export class SearchApiService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = environment.apiBaseUrl;

  /**
   * GET /api/search. Returns an empty group list synchronously when
   * the query is shorter than the minimum length so the component can
   * skip the round-trip and render the recents view instead.
   */
  search(query: string, limit = 5): Observable<ReadonlyArray<SearchGroup>> {
    const trimmed = query.trim();
    if (trimmed.length < MIN_QUERY_LENGTH) {
      return of([]);
    }

    const params = new HttpParams()
      .set('q', trimmed)
      .set('limit', String(limit));

    return this.http
      .get<SearchResponseWireDto>(`${this.baseUrl}/api/search`, { params })
      .pipe(map(response => response.groups.map(toGroup)));
  }
}

function toGroup(g: SearchGroupWireDto): SearchGroup {
  return {
    id: g.id as SearchGroupId,
    label: g.label,
    hits: g.hits.map(toHit),
  };
}

function toHit(h: SearchHitWireDto): SearchHit {
  return {
    id: h.id,
    group: '' as SearchGroupId,   // not used on the wire; group ownership is via SearchGroup.id
    title: h.title,
    meta: h.meta,
    icon: h.icon,
    navigate: h.navigate ?? undefined,
  };
}
