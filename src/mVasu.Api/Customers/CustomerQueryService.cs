using System.Globalization;
using System.Security.Claims;
using mVasu.Api.Contracts;

namespace mVasu.Api.Customers;

/// <summary>
/// Phase-1 mock implementation of <see cref="ICustomerQueryService"/>.
/// Mirrors the synthetic dataset the PWA used to ship in
/// <c>features/customers/customers.service.ts</c> — same ids, same
/// counts, same tags — so the contract is locked across all three
/// customer types and the rare zero-count edge case.
/// </summary>
public sealed class CustomerQueryService : ICustomerQueryService
{
    private static readonly Comparison<CustomerDto> ByDisplayNameFi =
        (a, b) => string.Compare(
            a.DisplayName, b.DisplayName,
            CultureInfo.GetCultureInfo("fi-FI"),
            CompareOptions.IgnoreNonSpace | CompareOptions.IgnoreCase);

    public Task<CustomersResponseDto> ListAsync(
        ClaimsPrincipal principal,
        CustomerQueryParameters query,
        CancellationToken cancellationToken = default)
    {
        var needle = query.Search?.Trim() ?? string.Empty;

        var matches = MOCK
            .Where(c => MatchesType(c, query.Type))
            .Where(c => MatchesCity(c, query.City))
            .Where(c => MatchesRelation(c, query.Relation))
            .Where(c => MatchesQuery(c, needle))
            .ToList();

        matches.Sort(ByDisplayNameFi);

        return Task.FromResult(new CustomersResponseDto(matches, matches.Count));
    }

    private static bool MatchesType(CustomerDto c, string? type) =>
        string.IsNullOrWhiteSpace(type) || c.Type == type;

    private static bool MatchesCity(CustomerDto c, string? city) =>
        string.IsNullOrWhiteSpace(city) || c.City == city;

    private static bool MatchesRelation(CustomerDto c, string? relation)
    {
        if (string.IsNullOrWhiteSpace(relation)) return true;
        return relation switch
        {
            CustomerRelationFilters.HasContract    => c.Counts.Contracts    > 0,
            CustomerRelationFilters.HasApplication => c.Counts.Applications > 0,
            CustomerRelationFilters.HasOffer       => c.Counts.Offers       > 0,
            CustomerRelationFilters.HasShowing     => c.Counts.Showings     > 0,
            CustomerRelationFilters.NoRelations    => Total(c.Counts) == 0,
            _                                      => true,
        };
    }

    private static int Total(CustomerCountsDto c) =>
        c.Applications + c.Reservations + c.Contracts + c.Offers + c.Showings;

    private static bool MatchesQuery(CustomerDto c, string needle)
    {
        if (needle.Length == 0) return true;
        if (Contains(c.DisplayName, needle))      return true;
        if (Contains(c.PrimaryAddress, needle))   return true;
        if (Contains(c.City, needle))             return true;
        if (Contains(c.Phone, needle))            return true;
        if (Contains(c.Email, needle))            return true;
        if (Contains(c.BusinessId, needle))       return true;
        if (Contains(c.ParentCompanyName, needle))return true;
        return false;
    }

    private static bool Contains(string? haystack, string needle) =>
        haystack is not null
        && haystack.IndexOf(needle, StringComparison.CurrentCultureIgnoreCase) >= 0;

    private static CustomerCountsDto Counts(
        int applications = 0, int reservations = 0, int contracts = 0,
        int offers = 0, int showings = 0) =>
        new(applications, reservations, contracts, offers, showings);

    // ── Henkilo + Yritys + Yhteyshenkilo mock dataset ──────────────────
    // Names follow the Org rule (no real customer identifiers); the data
    // mirrors what the PWA shipped in CustomersService for parity.
    private static readonly IReadOnlyList<CustomerDto> MOCK = new CustomerDto[]
    {
        // ── Henkilö-asiakkaat (active tenants & various) ──
        Person("c-001", "AE", "Aalto",     "Eero",
            primaryAddress: "Mannerheimintie 12 A 4", city: "Helsinki",
            counts: Counts(contracts: 1)),

        Person("c-002", "AH", "Aro",       "Hilkka",
            primaryAddress: "Vänrikinkatu 2", city: "Helsinki",
            counts: Counts(contracts: 1, applications: 1),
            tag: new CustomerTagDto("Etsii uutta", "info")),

        Person("c-003", "EE", "Esimerkki", "Eemeli",
            primaryAddress: "Aleksanterinkatu 12", city: "Helsinki",
            counts: Counts(contracts: 1)),

        Person("c-004", "HI", "Halonen",   "Inkeri",
            primaryAddress: "Hämeenkatu 7", city: "Tampere",
            counts: Counts(contracts: 1)),

        Person("c-005", "HA", "Heikkinen", "Antti",
            primaryAddress: "Kaisaniemenkatu 3", city: "Helsinki",
            counts: Counts(contracts: 1),
            tag: new CustomerTagDto("Päättyy 30.6.", "info")),

        Person("c-006", "HT", "Holopainen","Tuomas",
            primaryAddress: "Maauunintie 23 A 2", city: "Vantaa",
            counts: Counts(contracts: 1, showings: 1)),

        Person("c-007", "JL", "Jokinen",   "Liisa",
            primaryAddress: "Asemakuja 1 B 69", city: "Espoo",
            counts: Counts(contracts: 1, reservations: 1),
            tag: new CustomerTagDto("Varaus aktiivinen", "info")),

        Person("c-008", "KM", "Kallio",    "Marja",
            primaryAddress: "Mannerheimintie 12 B 7", city: "Helsinki",
            counts: Counts(contracts: 1)),

        Person("c-009", "KP", "Karhu",     "Pekka",
            primaryAddress: "Tehtaankatu 8", city: "Helsinki",
            counts: Counts(contracts: 1)),

        Person("c-010", "KS", "Koivula",   "Saara",
            primaryAddress: "Kauppakatu 14", city: "Lahti",
            counts: Counts(contracts: 1, offers: 1),
            tag: new CustomerTagDto("Allekirjoitus", "cta")),

        Person("c-011", "KA", "Korhonen",  "Anna",
            primaryAddress: "Kalevankatu 22", city: "Tampere",
            counts: Counts(contracts: 1)),

        Person("c-012", "LJ", "Laakso",    "Jukka",
            primaryAddress: "Mannerheimintie 12 C 11", city: "Helsinki",
            counts: Counts(contracts: 1)),

        Person("c-013", "MO", "Mäkelä",    "Olli",
            primaryAddress: "Pursimiehenkatu 4", city: "Helsinki",
            counts: Counts(contracts: 1, reservations: 1)),

        Person("c-014", "NA", "Niemi",     "Anneli",
            primaryAddress: "Asemakuja 1 B 12", city: "Espoo",
            counts: Counts(contracts: 1)),

        Person("c-015", "OH", "Oksanen",   "Helena",
            primaryAddress: "Kasarmikatu 19", city: "Helsinki",
            counts: Counts(contracts: 1)),

        // Pure applicants — no contract yet
        Person("c-101", "AM", "Ahonen",    "Matti",
            city: "Helsinki",
            counts: Counts(applications: 1, offers: 1),
            tag: new CustomerTagDto("Allekirjoitus", "cta")),

        Person("c-102", "ET", "Enckell",   "Tiia",
            city: "Lappeenranta",
            counts: Counts(applications: 1),
            tag: new CustomerTagDto("Uusi", "info")),

        Person("c-103", "HI", "Halme",     "Iida",
            city: "Oulu",
            counts: Counts(applications: 1, showings: 1),
            tag: new CustomerTagDto("Tutustumiskäynti tänään", "info")),

        Person("c-104", "HP", "Heinonen",  "Pasi",
            city: "Espoo",
            counts: Counts(applications: 1, showings: 1)),

        Person("c-105", "HH", "Huhtikuu",  "Heikki",
            city: "Helsinki",
            counts: Counts(applications: 1, showings: 2)),

        Person("c-106", "IA", "Ikonen",    "Ansa",
            city: "Espoo",
            counts: Counts(reservations: 1, offers: 1),
            tag: new CustomerTagDto("Verkkokauppa", "info")),

        Person("c-107", "JS", "Jätkä",     "Saara",
            city: "Helsinki",
            counts: Counts(applications: 1)),

        Person("c-108", "KK", "Koivuranta","Kaija",
            city: "Oulu",
            counts: Counts(applications: 1)),

        Person("c-109", "PH", "Pajunen",   "Hanna",
            city: "Helsinki",
            counts: Counts(applications: 1, offers: 1),
            tag: new CustomerTagDto("Allekirjoitus", "cta")),

        Person("c-110", "TM", "Testaaja",  "Maaliskuu",
            city: "Mäntsälä",
            counts: Counts(applications: 1)),

        // Former tenants — no active relations
        Person("c-201", "AT", "Aurinkoinen","Tuutikki",
            city: "Helsinki",
            counts: Counts(),
            tag: new CustomerTagDto("Päättynyt 31.3.", "default")),

        Person("c-202", "GS", "Granberg",  "Satu",
            city: "Tampere",
            counts: Counts(),
            tag: new CustomerTagDto("Päättynyt", "default")),

        // Contact-only — rare edge case (no relations at all)
        Person("c-301", "YK", "Yhteystieto","Kirjattu",
            city: "Helsinki",
            counts: Counts()),

        // ── Yritys-asiakkaat ──
        Company("c-y01", "LT", "Lumo Tekniikka Oy", businessId: "2345678-9",
            primaryAddress: "Toimitilakuja 5", city: "Helsinki",
            counts: Counts(contracts: 1)),

        Company("c-y02", "EO", "Esimerkki Oy", businessId: "1112223-4",
            primaryAddress: "Yritystie 12", city: "Espoo",
            counts: Counts(contracts: 1, applications: 1)),

        Company("c-y03", "TA", "Testaaja Asunnot Oy", businessId: "4456778-1",
            city: "Tampere",
            counts: Counts(applications: 1, offers: 1),
            tag: new CustomerTagDto("Toimitilatarjous", "cta")),

        Company("c-y04", "MO", "Mallikohde Oyj", businessId: "5567889-3",
            primaryAddress: "Hämeenkatu 22", city: "Tampere",
            counts: Counts(contracts: 2)),

        Company("c-y05", "PY", "Placeholder Yritys Oy",
            city: "Helsinki",
            counts: Counts()),

        // ── Yhteyshenkilo-asiakkaat ──
        ContactPerson("c-yh01", "VM", "Virtanen", "Maija",
            parentCompanyId: "c-y01", parentCompanyName: "Lumo Tekniikka Oy",
            city: "Helsinki", phone: "044 PLACEHOLDER",
            counts: Counts(),
            tag: new CustomerTagDto("Päätösoikeus", "default")),

        ContactPerson("c-yh02", "SP", "Salo",     "Pekka",
            parentCompanyId: "c-y02", parentCompanyName: "Esimerkki Oy",
            city: "Espoo",
            counts: Counts(applications: 1)),

        ContactPerson("c-yh03", "LJ", "Lampila",  "Juhani",
            parentCompanyId: "c-y03", parentCompanyName: "Testaaja Asunnot Oy",
            city: "Tampere",
            counts: Counts(offers: 1),
            tag: new CustomerTagDto("Allekirjoitus", "cta")),

        ContactPerson("c-yh04", "ME", "Mäki",     "Eeva",
            parentCompanyId: "c-y04", parentCompanyName: "Mallikohde Oyj",
            city: "Tampere",
            counts: Counts()),

        ContactPerson("c-yh05", "NK", "Niinikoski","Kalle",
            parentCompanyId: "c-y04", parentCompanyName: "Mallikohde Oyj",
            city: "Tampere",
            counts: Counts()),
    };

    private static CustomerDto Person(
        string id, string initials, string lastName, string firstName,
        CustomerCountsDto counts,
        string? primaryAddress = null, string? city = null,
        CustomerTagDto? tag = null,
        string? phone = null, string? email = null) =>
        new(
            Id: id,
            Type: CustomerTypes.Person,
            DisplayName: $"{lastName}, {firstName}",
            Initials: initials,
            Counts: counts,
            PrimaryAddress: primaryAddress,
            City: city,
            Tag: tag,
            Phone: phone,
            Email: email,
            FirstName: firstName,
            LastName: lastName,
            CompanyName: null,
            BusinessId: null,
            ParentCompanyId: null,
            ParentCompanyName: null);

    private static CustomerDto Company(
        string id, string initials, string companyName,
        CustomerCountsDto counts,
        string? businessId = null,
        string? primaryAddress = null, string? city = null,
        CustomerTagDto? tag = null,
        string? phone = null, string? email = null) =>
        new(
            Id: id,
            Type: CustomerTypes.Company,
            DisplayName: companyName,
            Initials: initials,
            Counts: counts,
            PrimaryAddress: primaryAddress,
            City: city,
            Tag: tag,
            Phone: phone,
            Email: email,
            FirstName: null,
            LastName: null,
            CompanyName: companyName,
            BusinessId: businessId,
            ParentCompanyId: null,
            ParentCompanyName: null);

    private static CustomerDto ContactPerson(
        string id, string initials, string lastName, string firstName,
        string parentCompanyId, string parentCompanyName,
        CustomerCountsDto counts,
        string? city = null,
        CustomerTagDto? tag = null,
        string? phone = null, string? email = null) =>
        new(
            Id: id,
            Type: CustomerTypes.ContactPerson,
            DisplayName: $"{lastName}, {firstName}",
            Initials: initials,
            Counts: counts,
            PrimaryAddress: null,
            City: city,
            Tag: tag,
            Phone: phone,
            Email: email,
            FirstName: firstName,
            LastName: lastName,
            CompanyName: null,
            BusinessId: null,
            ParentCompanyId: parentCompanyId,
            ParentCompanyName: parentCompanyName);
}
