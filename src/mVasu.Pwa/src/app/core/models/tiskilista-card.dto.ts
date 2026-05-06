/**
 * Mirrors mVasu.Api.Contracts.TiskilistaCardDto. Vuokra is a number (XPO
 * stores it as double), neliot is a number (XPO single), kerros/kerroksia
 * are free-form strings ("2/4" etc.).
 */
export interface TiskilistaCardDto {
  readonly id: string;
  readonly osoite: string;
  readonly kptunnus: number | null;
  readonly huonetunnus: number | null;
  readonly tyyppi: string | null;
  readonly laji: string | null;
  readonly vuokra: number | null;
  readonly vapautuu: string | null;
  readonly neliot: number | null;
  readonly kerros: string | null;
  readonly kerroksia: string | null;
  readonly tila: string | null;
  readonly sopimusTila: string | null;
  readonly kunta: string | null;
  readonly kaupunginosa: string | null;
  readonly prio: string | null;
  readonly isannoitsija: string | null;
  readonly markkinoija: string | null;
  readonly lumoFi: boolean;
  readonly vuokraovi: boolean;
  readonly onKuvausTarve: boolean;
  readonly hissi: boolean;
  readonly parveke: boolean;
  readonly sauna: boolean;
  readonly nextEsittelyAt: string | null;
  readonly latitude: number | null;
  readonly longitude: number | null;
  readonly distanceKm: number | null;
}
