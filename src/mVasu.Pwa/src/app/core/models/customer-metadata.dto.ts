/**
 * Mirror of `mVasu.Api.Contracts.CustomerMetadataDto`. Loaded once
 * from `GET /api/customers/metadata` and used by the customer detail
 * view to render required-marker (`*`), read-only styling, maxLength
 * hints and appearance-rule highlights.
 *
 * Wire-side field names are camelCase identifiers that match the
 * Customer DTO (e.g. `lastName`, `postalCode`). Appearance kinds are
 * stable string ids that map to TypeScript helper functions in
 * `customers/henkilo-appearance.ts`.
 */

import type { CustomerType } from './customer.dto';

export interface CustomerFieldMetadata {
  readonly name: string;
  readonly displayName: string;
  readonly required: boolean;
  readonly readOnly: boolean;
  readonly maxLength: number | null;
  readonly mask: string | null;
  readonly maskType: string | null;
  readonly visibleInDetail: boolean;
}

export interface CustomerAppearanceRule {
  /** Stable id; the frontend dispatches to the matching evaluator. */
  readonly kind: string;
  readonly targetFields: ReadonlyArray<string>;
  /** SCSS class hint: 'appearance-error' | 'appearance-warn' | 'appearance-disabled' | 'appearance-hidden'. */
  readonly styleHint: string;
}

export interface CustomerTypeMetadata {
  readonly type: CustomerType;
  readonly fields: ReadonlyArray<CustomerFieldMetadata>;
  readonly appearanceRules: ReadonlyArray<CustomerAppearanceRule>;
}

export interface CustomerMetadataResponse {
  readonly person: CustomerTypeMetadata;
  readonly company: CustomerTypeMetadata;
  readonly contactPerson: CustomerTypeMetadata;
}
