using System.Reflection;
using DevExpress.Xpo;
using mVasu.Api.Contracts;
using xVasu.Data.Asma;

namespace mVasu.Api.Customers;

/// <summary>
/// Reads XAF / Model.xafml-relevant attributes off the customer entity
/// hierarchy once at construction time and exposes them as a wire-ready
/// <see cref="CustomerMetadataResponseDto"/>. Used by
/// <c>GET /api/customers/metadata</c> to feed the PWA detail view its
/// required-marker / read-only / maxLength / appearance hints.
/// </summary>
/// <remarks>
/// <para>Singleton-scoped: the metadata is derived from compiled
/// attributes that only change when the <c>xVasu.Module</c> NuGet
/// package is upgraded (i.e. on API rebuild). Caching once at startup
/// is correct.</para>
///
/// <para>Field naming bridges XAF PascalCase (e.g. <c>SukuNimi</c>) to
/// the camelCase names the wire DTO uses (<c>lastName</c>). The
/// mapping is hand-curated below — it's the same mapping
/// <see cref="XpoCustomerQueryService"/> implies in its mappers, kept
/// in one place so the PWA doesn't have to guess.</para>
///
/// <para>Appearance rules are reduced from raw XAF criterion strings
/// to a small set of stable kinds the frontend hardcodes evaluators
/// for — see the comment on <see cref="CustomerAppearanceRuleDto"/>.</para>
/// </remarks>
public sealed class CustomerMetadataService
{
    private readonly CustomerMetadataResponseDto _cache;

    public CustomerMetadataService()
    {
        _cache = new CustomerMetadataResponseDto(
            Person:        BuildFor(typeof(Henkilo),       CustomerTypes.Person),
            Company:       BuildFor(typeof(Yritys),        CustomerTypes.Company),
            ContactPerson: BuildFor(typeof(Yhteyshenkilo), CustomerTypes.ContactPerson));
    }

    public CustomerMetadataResponseDto GetMetadata() => _cache;

    private static CustomerTypeMetadataDto BuildFor(Type clrType, string wireType)
    {
        var fields = WireFieldMap(wireType)
            .Select(map => BuildField(clrType, map.Wire, map.Xaf))
            .Where(f => f is not null)
            .Cast<CustomerFieldMetadataDto>()
            .ToList();

        var appearance = BuildAppearanceRules(clrType, wireType).ToList();
        return new CustomerTypeMetadataDto(wireType, fields, appearance);
    }

    private static CustomerFieldMetadataDto? BuildField(Type clrType, string wireName, string xafName)
    {
        // Walk the inheritance chain — Asiakas-base properties (SukuNimi,
        // KatuOsoite, …) are inherited and need to be picked up too.
        PropertyInfo? prop = null;
        for (var t = clrType; t is not null && t != typeof(object); t = t.BaseType)
        {
            prop = t.GetProperty(xafName, BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);
            if (prop is not null) break;
        }
        if (prop is null) return null;

        var attrs = prop.GetCustomAttributesData();

        var displayName    = ReadString(attrs, "DisplayNameAttribute") ?? prop.Name;
        var required       = HasAttribute(attrs, "RuleRequiredFieldAttribute");
        var allowEdit      = ReadModelDefault(attrs, "AllowEdit");
        var readOnly       = string.Equals(allowEdit, "False", StringComparison.OrdinalIgnoreCase);
        var maxLength      = ReadSize(attrs);
        var mask           = ReadModelDefault(attrs, "EditMask");
        var maskType       = ReadModelDefault(attrs, "EditMaskType");
        var visibleInDetailRaw = ReadBool(attrs, "VisibleInDetailViewAttribute");
        // Default in XAF: visible unless explicitly false.
        var visibleInDetail = visibleInDetailRaw ?? true;

        return new CustomerFieldMetadataDto(
            Name: wireName,
            DisplayName: displayName,
            Required: required,
            ReadOnly: readOnly,
            MaxLength: maxLength,
            Mask: mask,
            MaskType: maskType,
            VisibleInDetail: visibleInDetail);
    }

    /// <summary>
    /// Wire→XAF property mapping. Per-type because Henkilo/Yritys/
    /// Yhteyshenkilo expose different subset of fields. Keep in sync
    /// with the wire shape <see cref="XpoCustomerQueryService"/>
    /// produces.
    /// </summary>
    private static IReadOnlyList<(string Wire, string Xaf)> WireFieldMap(string wireType) => wireType switch
    {
        CustomerTypes.Person => new[]
        {
            ("firstName",                "EtuNimi"),
            ("lastName",                 "SukuNimi"),
            ("primaryAddress",           "KatuOsoite"),
            ("postalCode",               "PostiNumero"),
            ("city",                     "PostiToimiPaikka"),
            ("country",                  "Maa"),
            ("phone",                    "Gsm"),
            ("email",                    "Email"),
            ("language",                 "LangCode"),
            ("profession",               "Ammatti"),
            ("industry",                 "ToimiAla"),
            ("workplace",                "TyoPaikka"),
            ("income",                   "BruttoTulot"),
            ("emailMarketingAllowed",    "EmailKayttoSallittu"),
            ("phoneMarketingAllowed",    "PuhNoKayttoSallittu"),
            ("directMarketingForbidden", "Suoramarkkinointikielto"),
        },

        CustomerTypes.Company => new[]
        {
            ("companyName",              "SukuNimi"),
            ("businessId",               "CompanyID"),
            ("primaryAddress",           "KatuOsoite"),
            ("postalCode",               "PostiNumero"),
            ("city",                     "PostiToimiPaikka"),
            ("country",                  "Maa"),
            ("phone",                    "Gsm"),
            ("email",                    "Email"),
            ("language",                 "LangCode"),
            ("industry",                 "ToimiAla"),
            ("emailMarketingAllowed",    "EmailKayttoSallittu"),
            ("phoneMarketingAllowed",    "PuhNoKayttoSallittu"),
            ("directMarketingForbidden", "Suoramarkkinointikielto"),
        },

        CustomerTypes.ContactPerson => new[]
        {
            ("firstName",                "EtuNimi"),
            ("lastName",                 "SukuNimi"),
            ("primaryAddress",           "KatuOsoite"),
            ("postalCode",               "PostiNumero"),
            ("city",                     "PostiToimiPaikka"),
            ("country",                  "Maa"),
            ("phone",                    "Gsm"),
            ("email",                    "Email"),
            ("language",                 "LangCode"),
            ("emailMarketingAllowed",    "EmailKayttoSallittu"),
            ("phoneMarketingAllowed",    "PuhNoKayttoSallittu"),
            ("directMarketingForbidden", "Suoramarkkinointikielto"),
        },

        _ => Array.Empty<(string, string)>(),
    };

    /// <summary>
    /// Boil class-level XAF Appearance rules down to wire-friendly
    /// kinds — only the rules whose target fields are on the wire are
    /// surfaced; criterion strings are dropped and the frontend
    /// resolves them with hardcoded TypeScript helpers (per agreed
    /// plan: criterion engine is too much for this iteration).
    /// </summary>
    private static IEnumerable<CustomerAppearanceRuleDto> BuildAppearanceRules(Type clrType, string wireType)
    {
        // Asiakas-base rule: highlight Sukunimi when empty. Wire field
        // is "lastName" for Henkilo/Yhteyshenkilo, "companyName" for
        // Yritys (Yritysnimi alias on SukuNimi).
        yield return new CustomerAppearanceRuleDto(
            Kind: "asiakas.surname-empty",
            TargetFields: wireType == CustomerTypes.Company
                ? new[] { "companyName" }
                : new[] { "lastName" },
            StyleHint: "appearance-error");

        // Henkilö-specific rules. We surface those whose criteria the
        // frontend can evaluate today (no extra DTO fields needed) or
        // those whose target field already exists on the wire so the
        // frontend can wire them up when DTO grows.
        if (wireType != CustomerTypes.Person) yield break;

        // Both Henkilö rules below depend on fields that aren't on the
        // wire yet (LastOne, LastVRKQuery, OnEdunValvonta, OnNimenMuutoksia,
        // CanEditSSN). They're emitted as documentation so the frontend
        // can light them up the moment those columns appear in the DTO.
        yield return new CustomerAppearanceRuleDto(
            Kind: "henkilo.atpi-hairiot-high",
            TargetFields: Array.Empty<string>(),
            StyleHint: "appearance-error");

        yield return new CustomerAppearanceRuleDto(
            Kind: "henkilo.atpi-hairiot-mid",
            TargetFields: Array.Empty<string>(),
            StyleHint: "appearance-warn");

        yield return new CustomerAppearanceRuleDto(
            Kind: "henkilo.edunvalvonta",
            TargetFields: Array.Empty<string>(),
            StyleHint: "appearance-error");

        yield return new CustomerAppearanceRuleDto(
            Kind: "henkilo.nimenmuutoksia-disable",
            TargetFields: new[] { "firstName", "lastName" },
            StyleHint: "appearance-disabled");
    }

    // -- attribute helpers ------------------------------------------------

    private static bool HasAttribute(IList<CustomAttributeData> attrs, string name) =>
        attrs.Any(a => a.AttributeType.Name == name);

    private static bool? ReadBool(IList<CustomAttributeData> attrs, string attrName)
    {
        var a = attrs.FirstOrDefault(x => x.AttributeType.Name == attrName);
        if (a is null) return null;
        var arg = a.ConstructorArguments.FirstOrDefault();
        return arg.Value is bool b ? b : null;
    }

    private static string? ReadString(IList<CustomAttributeData> attrs, string attrName)
    {
        var a = attrs.FirstOrDefault(x => x.AttributeType.Name == attrName);
        if (a is null) return null;
        var arg = a.ConstructorArguments.FirstOrDefault();
        return arg.Value as string;
    }

    private static int? ReadSize(IList<CustomAttributeData> attrs)
    {
        var a = attrs.FirstOrDefault(x => x.AttributeType.Name is "SizeAttribute" or "FieldSizeAttribute");
        if (a is null) return null;
        var arg = a.ConstructorArguments.FirstOrDefault();
        return arg.Value is int i ? i : null;
    }

    private static string? ReadModelDefault(IList<CustomAttributeData> attrs, string key)
    {
        foreach (var a in attrs)
        {
            if (a.AttributeType.Name != "ModelDefaultAttribute") continue;
            if (a.ConstructorArguments.Count < 2) continue;
            if (a.ConstructorArguments[0].Value is string k
                && string.Equals(k, key, StringComparison.OrdinalIgnoreCase)
                && a.ConstructorArguments[1].Value is string v)
            {
                return v;
            }
        }
        return null;
    }
}
