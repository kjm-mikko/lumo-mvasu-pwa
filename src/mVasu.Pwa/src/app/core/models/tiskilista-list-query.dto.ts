export type TiskilistaScope = 'omat' | 'kaikki';
export type TiskilistaSortBy = 'vapautuu' | 'osoite' | 'vuokra' | 'distance';

export interface TiskilistaListQuery {
  readonly q: string | null;
  readonly status: string | null;
  readonly scope: TiskilistaScope;
  readonly sortBy: TiskilistaSortBy;
  readonly userLat: number | null;
  readonly userLon: number | null;
  readonly page: number;
  readonly pageSize: number;
}

export const DEFAULT_TISKILISTA_QUERY: TiskilistaListQuery = {
  q: null,
  status: null,
  scope: 'omat',
  sortBy: 'vapautuu',
  userLat: null,
  userLon: null,
  page: 1,
  pageSize: 20,
};
