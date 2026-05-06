namespace mVasu.Api.Contracts;

/// <summary>
/// Single customer (Asiakas) row for <c>GET /api/customers</c>. Maps to
/// one of three XAF entity types — <c>Henkilo</c>, <c>Yritys</c> or
/// <c>Yhteyshenkilo</c> (Yritys child). The <see cref="Type"/> field is
/// the discriminator; per-type fields (FirstName/LastName for persons,
/// CompanyName/BusinessId for companies, ParentCompany* for contact
/// persons) are optional at the wire level so the frontend can map
/// straight to its discriminated-union TypeScript shape.
/// </summary>
/// <remarks>
/// <para>"Roles" (asukas / hakija / entinen) are derived, not stored.
/// Each customer carries <see cref="Counts"/> covering active relations
/// — Sopimus / Hakemus / SopimusVaraus / Tarjous / Esittely — and the
/// caller renders them as count pills.</para>
/// </remarks>
public sealed record CustomerDto(
    string Id,
    /// <summary>One of <see cref="CustomerTypes"/>.</summary>
    string Type,
    /// <summary>fi-FI sortable display name. Henkilo / Yhteyshenkilo: "Sukunimi, Etunimi". Yritys: company name.</summary>
    string DisplayName,
    string Initials,
    CustomerCountsDto Counts,
    /// <summary>Active contract address if any (Sopimus.Huoneisto.Osoite).</summary>
    string? PrimaryAddress,
    string? City,
    CustomerTagDto? Tag,
    string? Phone,
    string? Email,
    // Henkilo / Yhteyshenkilo
    string? FirstName,
    string? LastName,
    // Yritys
    string? CompanyName,
    string? BusinessId,
    // Yhteyshenkilo
    string? ParentCompanyId,
    string? ParentCompanyName,
    // -- canonical detail-view fields (per Asiakas Model.xafml) --
    // List view leaves these null; the GetById endpoint populates them
    // when the detail UI calls in.
    /// <summary>Asiakas.PostiNumero — postinumero (Size 5, mask `[0-9]{1,5}`).</summary>
    string? PostalCode = null,
    /// <summary>Asiakas.Maa — maa, oletus piilossa listalla.</summary>
    string? Country = null,
    /// <summary>Asiakas.LangCode — viestintä-kieli (`fi`/`en`/...).</summary>
    string? Language = null,
    /// <summary>Henkilo.Ammatti — vapaa teksti (Size 50). Henkilö-only.</summary>
    string? Profession = null,
    /// <summary>Asiakas.ToimiAla — toimiala (Size 50). Henkilö/Yritys.</summary>
    string? Industry = null,
    /// <summary>Asiakas.TyoPaikka — työnantaja (Size 50). Henkilö-only.</summary>
    string? Workplace = null,
    /// <summary>Asiakas.BruttoTulot — bruttotulot €/vuosi.</summary>
    int? Income = null,
    /// <summary>Asiakas.EmailKayttoSallittu — saako lähettää sähköpostia.</summary>
    bool? EmailMarketingAllowed = null,
    /// <summary>Asiakas.PuhNoKayttoSallittu — saako soittaa.</summary>
    bool? PhoneMarketingAllowed = null,
    /// <summary>Asiakas.Suoramarkkinointikielto — kielto suoramarkkinoinnille.</summary>
    bool? DirectMarketingForbidden = null);
    // NOTE: Asiakas.InfoMessage is XPO-non-persistent and can't be
    // projected through SelectData. Reintroduce it as a derived banner
    // (e.g. "Asiakkaalla on perinnänestoja") computed from the related
    // flags if the UI needs it.

/// <summary>Aggregated active-relation counts. All values are non-negative.</summary>
public sealed record CustomerCountsDto(
    int Applications,   // Hakemus
    int Reservations,   // SopimusVaraus
    int Contracts,      // Sopimus (active only)
    int Offers,         // Tarjous (status awaiting signature)
    int Showings);      // Esittely / Tutustumiskaynti (upcoming)

/// <summary>
/// Optional status badge derived from the customer's relation state —
/// e.g. "Allekirjoitus" when an offer awaits signing, "Päättyy 30.6."
/// when a contract has a termination date within 30 days.
/// </summary>
public sealed record CustomerTagDto(
    string Label,
    /// <summary>One of "default" / "cta" / "info" / "warn".</summary>
    string Tone);

/// <summary>Top-level <c>GET /api/customers</c> envelope.</summary>
public sealed record CustomersResponseDto(
    IReadOnlyList<CustomerDto> Items,
    int Total);

/// <summary>Canonical customer-type discriminator values.</summary>
public static class CustomerTypes
{
    public const string Person        = "person";
    public const string Company       = "company";
    public const string ContactPerson = "contact-person";

    public static readonly IReadOnlySet<string> Valid =
        new HashSet<string> { Person, Company, ContactPerson };
}

/// <summary>Relation filters supported by the <c>relation</c> query param.</summary>
public static class CustomerRelationFilters
{
    public const string HasContract    = "has-contract";
    public const string HasApplication = "has-application";
    public const string HasOffer       = "has-offer";
    public const string HasShowing     = "has-showing";
    public const string NoRelations    = "no-relations";

    public static readonly IReadOnlySet<string> Valid =
        new HashSet<string> { HasContract, HasApplication, HasOffer, HasShowing, NoRelations };
}
