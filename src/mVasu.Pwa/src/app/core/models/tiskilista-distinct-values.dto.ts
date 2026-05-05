/**
 * Mirrors mVasu.Api.Contracts.TiskilistaDistinctValuesDto. Returned by
 * GET /api/tiskilista/distinct-values to populate filter dropdowns —
 * scoped to the user's BranchCode visibility, no other filters applied.
 */
export interface TiskilistaKuntaKaupunginosaDto {
  readonly kunta: string;
  readonly kaupunginosa: string;
}

export interface TiskilistaDistinctValuesDto {
  readonly lajit: ReadonlyArray<string>;
  readonly tyypit: ReadonlyArray<string>;
  readonly kunnat: ReadonlyArray<string>;
  readonly kaupunginosat: ReadonlyArray<string>;
  readonly kaupunginosatByKunta: ReadonlyArray<TiskilistaKuntaKaupunginosaDto>;
  readonly sopimustilat: ReadonlyArray<string>;
  readonly isannoitsijat: ReadonlyArray<string>;
  readonly markkinoijat: ReadonlyArray<string>;
  readonly tilat: ReadonlyArray<string>;
}

export const EMPTY_TISKILISTA_DISTINCT_VALUES: TiskilistaDistinctValuesDto = {
  lajit: [],
  tyypit: [],
  kunnat: [],
  kaupunginosat: [],
  kaupunginosatByKunta: [],
  sopimustilat: [],
  isannoitsijat: [],
  markkinoijat: [],
  tilat: [],
};
