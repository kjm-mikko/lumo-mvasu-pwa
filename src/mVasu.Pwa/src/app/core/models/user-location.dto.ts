/**
 * Mirrors mVasu.Api.Contracts.UserLocationDto.
 */
export interface UserLocationDto {
  readonly latitude: number;
  readonly longitude: number;
  readonly accuracy: number | null;
  readonly recordedAt: string;
}
