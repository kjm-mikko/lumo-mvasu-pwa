namespace mVasu.Api.Contracts;

/// <summary>
/// Field-level metadata distilled from XAF / Model.xafml attributes for
/// a customer-detail wire field. The PWA detail view reads this once
/// at startup and uses it to render required-marker (*), maxLength
/// hints and read-only styling.
/// </summary>
/// <remarks>
/// Names are wire-side camelCase identifiers that the frontend uses to
/// look up per-field metadata (e.g. <c>"sukunimi"</c>, <c>"postalCode"</c>).
/// Trying to keep this explicit instead of mapping back to XAF
/// PascalCase makes the frontend wiring straightforward.
/// </remarks>
public sealed record CustomerFieldMetadataDto(
    /// <summary>Wire-side camelCase identifier (e.g. "lastName", "postalCode").</summary>
    string Name,
    /// <summary>Localised display name (XAF DisplayName attribute, falls back to property name).</summary>
    string DisplayName,
    /// <summary>True when XAF <c>RuleRequiredField</c> applies — frontend marks the label with `*`.</summary>
    bool Required,
    /// <summary>True when XAF <c>ModelDefault("AllowEdit", "False")</c> is set — render dimmer.</summary>
    bool ReadOnly,
    /// <summary>String length cap (Size attribute) — passed as `maxlength` once we have edit support.</summary>
    int? MaxLength,
    /// <summary>EditMask regex pattern (e.g. <c>[0-9]{1,5}</c> for postal code).</summary>
    string? Mask,
    /// <summary>Mask flavour (RegEx, Standard, Numeric, …).</summary>
    string? MaskType,
    /// <summary>False when XAF <c>VisibleInDetailView=False</c> — frontend skips the row.</summary>
    bool VisibleInDetail);

/// <summary>
/// Class-level XAF Appearance rule converted into a wire-friendly tag.
/// Each rule names a target field, a hardcoded "kind" id (so the
/// frontend can dispatch to the right TypeScript helper) and a style
/// hint matching the SCSS class the detail view paints.
/// </summary>
/// <remarks>
/// We deliberately stop short of shipping the raw XAF criterion string —
/// the frontend can't evaluate `OnEdunValvonta` or `LastOne.Rating >= 8`
/// without pulling those fields onto the wire. Instead we ship a
/// stable Kind id and the frontend hardcodes the matching evaluator
/// (per the agreed plan).
/// </remarks>
public sealed record CustomerAppearanceRuleDto(
    /// <summary>Stable identifier (e.g. "henkilo.edunvalvonta", "asiakas.sukunimi-empty").</summary>
    string Kind,
    /// <summary>Wire-side field names this rule decorates.</summary>
    IReadOnlyList<string> TargetFields,
    /// <summary>SCSS class hint: "appearance-error" / "appearance-warn" / "appearance-disabled" / "appearance-hidden".</summary>
    string StyleHint);

/// <summary>
/// Per-customer-type metadata bundle. Sent under each type discriminator
/// in <see cref="CustomerMetadataResponseDto"/>.
/// </summary>
public sealed record CustomerTypeMetadataDto(
    /// <summary>Wire-side type discriminator (matches <see cref="CustomerTypes"/>).</summary>
    string Type,
    /// <summary>Field metadata keyed by wire name.</summary>
    IReadOnlyList<CustomerFieldMetadataDto> Fields,
    IReadOnlyList<CustomerAppearanceRuleDto> AppearanceRules);

/// <summary>
/// Top-level <c>GET /api/customers/metadata</c> envelope. One bundle
/// per customer type — the frontend keeps them in a service signal and
/// looks up the relevant one per detail render.
/// </summary>
public sealed record CustomerMetadataResponseDto(
    CustomerTypeMetadataDto Person,
    CustomerTypeMetadataDto Company,
    CustomerTypeMetadataDto ContactPerson);
