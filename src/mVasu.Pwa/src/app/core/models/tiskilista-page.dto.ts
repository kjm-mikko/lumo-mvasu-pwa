import type { TiskilistaCardDto } from './tiskilista-card.dto';

export interface TiskilistaPageDto {
  readonly items: ReadonlyArray<TiskilistaCardDto>;
  readonly total: number;
  readonly page: number;
  readonly pageSize: number;
}
