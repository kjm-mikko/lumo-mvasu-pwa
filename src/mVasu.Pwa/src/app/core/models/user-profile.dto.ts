/**
 * Mirrors mVasu.Api.Contracts.UserProfileDto. Keep field order in sync so a
 * future TS code-generator from the OpenAPI document produces identical output.
 */
export interface UserProfileDto {
  readonly id: string;
  readonly email: string;
  readonly displayName: string;
  readonly preferredName: string | null;
  readonly theme: 'light' | 'dark' | 'system' | string;
  readonly language: 'fi' | 'en' | string;
  readonly locationConsent: boolean;
}
