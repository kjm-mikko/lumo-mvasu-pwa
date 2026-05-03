/**
 * People-tab list row (SCREENS.md §02). Matches the shape that
 * BACKEND.md /api/people will produce; today the data comes from a
 * local fixture in PeopleService.
 *
 * The `name` field follows the XAF "Sukunimi, Etunimi" convention so
 * Finnish-locale alphabetical sort with `Intl.Collator('fi-FI')` works
 * directly without extra parsing.
 */

export type PersonRole = 'tenant' | 'applicant' | 'former';

export interface PersonTag {
  readonly label: string;
  readonly tone: 'default' | 'cta' | 'info';
}

export interface Person {
  readonly id: string;
  readonly name: string;            // "Sukunimi, Etunimi"
  readonly initials: string;        // 2-char avatar
  readonly meta: string;            // "Asukas · M-katu 8 B 12"
  readonly role: PersonRole;
  readonly city?: string;
  readonly contractType?: string;
  readonly tag?: PersonTag;
  readonly phone?: string;
  readonly email?: string;
}

export interface PersonFilters {
  readonly role?: PersonRole;
  readonly city?: string;
}

export const PERSON_ROLE_LABELS: Record<PersonRole, string> = {
  tenant:    'Asukkaat',
  applicant: 'Hakijat',
  former:    'Entiset asukkaat',
};
