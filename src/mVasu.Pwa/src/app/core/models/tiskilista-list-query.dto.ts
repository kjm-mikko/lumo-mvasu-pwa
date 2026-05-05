export type TiskilistaScope = 'omat' | 'kaikki';
export type TiskilistaSortBy = 'vapautuu' | 'osoite' | 'vuokra' | 'distance';

/**
 * Known categorical values for the `laji` filter. The list mirrors the
 * distinct values seen in the live Tiskilista table; the API still
 * accepts any string so additional values can be plumbed without a
 * frontend change.
 */
export const TISKILISTA_LAJI_OPTIONS: ReadonlyArray<string> = [
  'Asuinhuoneisto',
  'Toimitila',
  'Muutila',
  'Ei vuokrattavat tilat',
  'Asuinh.liittyvät vuokr.ti',
];

export interface TiskilistaListQuery {
  readonly q: string | null;
  readonly status: string | null;
  readonly lajit: ReadonlyArray<string>;
  readonly tyypit: ReadonlyArray<string>;
  readonly kunnat: ReadonlyArray<string>;
  readonly kaupunginosat: ReadonlyArray<string>;
  readonly sopimustilat: ReadonlyArray<string>;
  readonly isannoitsijat: ReadonlyArray<string>;
  readonly markkinoijat: ReadonlyArray<string>;
  readonly neliotMin: number | null;
  readonly neliotMax: number | null;
  /** ISO date (yyyy-MM-dd) — server parses as DateOnly. */
  readonly vapautuuFrom: string | null;
  readonly vapautuuTo: string | null;
  readonly onKuvausTarveOnly: boolean;
  readonly lumoFiOnly: boolean;
  readonly hasUpcomingEsittelyOnly: boolean;
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
  lajit: [],
  tyypit: [],
  kunnat: [],
  kaupunginosat: [],
  sopimustilat: [],
  isannoitsijat: [],
  markkinoijat: [],
  neliotMin: null,
  neliotMax: null,
  vapautuuFrom: null,
  vapautuuTo: null,
  onKuvausTarveOnly: false,
  lumoFiOnly: false,
  hasUpcomingEsittelyOnly: false,
  scope: 'omat',
  sortBy: 'vapautuu',
  userLat: null,
  userLon: null,
  page: 1,
  pageSize: 20,
};
