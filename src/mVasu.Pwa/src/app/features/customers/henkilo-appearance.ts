import type { Customer, PersonCustomer } from '../../core/models/customer.dto';

/**
 * Hardcoded TypeScript evaluators for XAF Appearance rules that
 * decorate the Henkilö detail view. Each function tests whether a
 * specific rule fires for a given customer; the detail component
 * combines that with the SCSS class hint shipped from the metadata
 * service.
 *
 * We keep the rules hardcoded (not a generic XAF criterion engine)
 * per the agreed plan — the criterion language is rich enough that
 * a half-finished evaluator would be more dangerous than the manual
 * mapping below. Add a new rule here whenever the wire DTO grows the
 * field a XAF criterion references.
 *
 * Reference: `xVasuReflect --xaf-rules xVasu.Data.Asma.Henkilo`
 * (class-level Appearance rules section).
 */

/**
 * Asiakas-base rule:
 *   `Appearance("Asiakas.RuleRequiredField.Highlight",
 *     FontColor=Maroon, BackColor=Red, TargetItems=SukuNimi,
 *     Criteria="IsNull([SukuNimi])")`
 *
 * Frontend translation: highlight the surname / company-name cell
 * when the underlying XAF SukuNimi column is blank.
 */
export function isSurnameMissing(c: Customer): boolean {
  if (c.type === 'company') return !c.companyName?.trim();
  return !c.lastName?.trim();
}

/**
 * Henkilö rule placeholders. The wire DTO doesn't yet carry
 * `OnEdunValvonta`, `OnNimenMuutoksia`, `LastOne.Rating` or
 * `LastVRKQuery`, so these always return false today. They exist as
 * named extension points so the detail view can flip them on the
 * moment those fields land in CustomerDto.
 */

export function isUnderEdunvalvonta(_c: PersonCustomer): boolean {
  // TODO: enable once Henkilo.OnEdunValvonta lands on the wire.
  return false;
}

export function hasNimenMuutoksia(_c: PersonCustomer): boolean {
  // TODO: enable once Henkilo.OnNimenMuutoksia lands on the wire.
  return false;
}

export function atpiHairiotHigh(_c: PersonCustomer): boolean {
  // TODO: enable once LastOne (AsiakasLuottokysely2) lands on the wire.
  return false;
}

export function atpiHairiotMid(_c: PersonCustomer): boolean {
  // TODO: enable once LastOne lands on the wire.
  return false;
}
