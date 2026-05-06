import type { CustomerFieldMetadata } from '../models/customer-metadata.dto';

/**
 * Indexes a metadata bundle's field array by wire name so the detail
 * template can look up `meta[fieldName]` in O(1) instead of scanning
 * the array per render.
 */
export function mapByName(
  fields: ReadonlyArray<CustomerFieldMetadata>,
): Readonly<Record<string, CustomerFieldMetadata>> {
  const out: Record<string, CustomerFieldMetadata> = {};
  for (const f of fields) out[f.name] = f;
  return out;
}
