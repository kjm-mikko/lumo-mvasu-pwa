/**
 * Asiakkaat-tab list row (SCREENS.md §02 + Lumo domain clarification).
 *
 * "Asiakas" maps to one of three XAF entity types under
 * `xVasu.Data.Asma`:
 *
 *   - `Henkilo`        — natural person (Etunimi + Sukunimi)
 *   - `Yritys`         — legal entity (company name + Y-tunnus)
 *   - `Yhteyshenkilo`  — contact person inside a Yritys (firstName +
 *                        lastName + parent Yritys reference)
 *
 * Henkilo and Yritys share an abstract parent (Sopimusasiakas);
 * Sopimus / Hakemus discriminate them at runtime via `OnHenkilo` /
 * `OnYritys` boolean accessors (see Model.xafml AppearanceRules).
 *
 * A customer's "role" is **derived** from related entities, not stored:
 *
 *   - `Hakemus`        — applications / leads, Hakemustyyppi varies
 *   - `SopimusVaraus`  — contract reservations
 *   - `Sopimus`        — active contracts → "asukas / vuokralainen"
 *   - `Tarjous`        — open offers awaiting signature
 *   - `Esittely` /     — scheduled showings
 *     `Tutustumiskaynti`
 *
 * A customer can have any combination — including none at all (rare
 * contact-only records). The counts surfaced on each row let the user
 * triage at a glance without opening the detail.
 *
 * The future BACKEND.md `/api/customers` endpoint should return this
 * exact shape; the mock fixture in CustomersService produces it today.
 */

export type CustomerType = 'person' | 'company' | 'contact-person';

export interface CustomerCounts {
  readonly applications: number;   // Hakemus
  readonly reservations: number;   // SopimusVaraus
  readonly contracts: number;      // Sopimus (active only)
  readonly offers: number;         // Tarjous (status awaiting signature)
  readonly showings: number;       // Esittely / Tutustumiskaynti (upcoming)
}

export type CustomerRelationFilter =
  | 'has-contract'
  | 'has-application'
  | 'has-offer'
  | 'has-showing'
  | 'no-relations';

export interface CustomerTag {
  readonly label: string;
  readonly tone: 'default' | 'cta' | 'info' | 'warn';
}

interface BaseCustomer {
  readonly id: string;
  readonly type: CustomerType;
  /** Used for fi-FI sort and avatar derivation across all types. */
  readonly displayName: string;
  readonly initials: string;
  readonly counts: CustomerCounts;
  readonly primaryAddress?: string;
  readonly city?: string;
  readonly tag?: CustomerTag;
  readonly phone?: string;
  readonly email?: string;

  // Canonical detail-view fields. The list endpoint leaves these undefined;
  // GetById fills them in. Names mirror Asiakas Model.xafml:
  //   PostiNumero, Maa, LangCode, ToimiAla, TyoPaikka, BruttoTulot,
  //   EmailKayttoSallittu, PuhNoKayttoSallittu, Suoramarkkinointikielto,
  //   InfoMessage. PII (PersonID/SSN/DOB/Age) is deliberately not on the
  //   wire.
  readonly postalCode?: string;
  readonly country?: string;
  readonly language?: string;
  readonly industry?: string;
  /** Henkilö-only — Henkilo.Ammatti. */
  readonly profession?: string;
  /** Henkilö-only — Asiakas.TyoPaikka. */
  readonly workplace?: string;
  /** Asiakas.BruttoTulot — bruttotulot €/vuosi. */
  readonly income?: number;
  readonly emailMarketingAllowed?: boolean;
  readonly phoneMarketingAllowed?: boolean;
  readonly directMarketingForbidden?: boolean;
}

export interface PersonCustomer extends BaseCustomer {
  readonly type: 'person';
  readonly firstName: string;       // Henkilo.EtuNimi
  readonly lastName: string;        // Henkilo.Sukunimi
}

export interface CompanyCustomer extends BaseCustomer {
  readonly type: 'company';
  readonly companyName: string;     // Yritys.Nimi
  readonly businessId?: string;     // Y-tunnus
}

export interface ContactPersonCustomer extends BaseCustomer {
  readonly type: 'contact-person';
  readonly firstName: string;
  readonly lastName: string;
  /** Foreign key to the parent Yritys. */
  readonly parentCompanyId: string;
  /** Cached parent company name; rendered as the contact's secondary line. */
  readonly parentCompanyName: string;
}

export type Customer = PersonCustomer | CompanyCustomer | ContactPersonCustomer;

export interface CustomerFilters {
  readonly type?: CustomerType;
  readonly relation?: CustomerRelationFilter;
  readonly city?: string;
}

export const CUSTOMER_TYPE_LABELS: Record<CustomerType, string> = {
  person:           'Henkilöt',
  company:          'Yritykset',
  'contact-person': 'Yhteyshenkilöt',
};

export const CUSTOMER_RELATION_LABELS: Record<CustomerRelationFilter, string> = {
  'has-contract':    'Aktiivinen sopimus',
  'has-application': 'Avoin hakemus',
  'has-offer':       'Avoin tarjous',
  'has-showing':     'Tuleva esittely',
  'no-relations':    'Ei aktiivisia liitoksia',
};

export function totalCount(c: CustomerCounts): number {
  return c.applications + c.reservations + c.contracts + c.offers + c.showings;
}
