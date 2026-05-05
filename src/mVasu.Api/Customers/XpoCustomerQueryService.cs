using System.Globalization;
using System.Security.Claims;
using DevExpress.Data.Filtering;
using DevExpress.ExpressApp;
using DevExpress.ExpressApp.Xpo;
using DevExpress.Xpo;
using mVasu.Api.Authentication;
using mVasu.Api.Contracts;
using xVasu.Data.Asma;
using xVasu.Data.Security;
using XpoContract = xVasu.Data.Vuha.Sopimus;
using XpoContractAsiakas = xVasu.Data.Vuha.SopimusAsiakas;
using XpoApplicationAsiakas = xVasu.Data.Asma.HakemusAsiakas;
using XpoReservation = xVasu.Data.Varaus.AsiakasVaraus;
using XpoInspection = xVasu.Data.DirectRent.DirectRentalCustomerInspection;

namespace mVasu.Api.Customers;

/// <summary>
/// XPO-backed implementation of <see cref="ICustomerQueryService"/>.
/// Reads <c>xVasu.Data.Asma.Asiakas</c> (and its three concrete subtypes
/// — Henkilo / Yritys / Yhteyshenkilo) and aggregates per-customer counts
/// for the relations that drive the count pills.
/// </summary>
/// <remarks>
/// <para>Visibility: the service deliberately applies no <c>BranchCode</c>
/// scope filter — the XPO security session is opened in the user's name
/// (<see cref="EmailResolver"/>), so XAF's PermissionPolicy filters rows
/// out at the persistence layer for users who lack access. This matches
/// the org's existing entity-level ACLs on <c>xVasu.Data.Asma</c>.</para>
///
/// <para>PII handling: <c>Henkilo.PersonID</c> (sotu), <c>DOB</c> and
/// <c>Age</c> are NEVER projected to the wire — only display name,
/// address parts, email and phone leave the service.</para>
///
/// <para>Counts: aggregated via four batch <see cref="InOperator"/>
/// queries against the page's customer numbers (Sopimus / Hakemus /
/// SopimusVaraus / DirectRentalCustomerInspection). The Offers count
/// stays at 0 in this phase — it overlaps SopimusVaraus and we don't
/// yet have the SopimusVarausTila tunnus that distinguishes "tarjous
/// odottaa allekirjoitusta" from a generic reservation (OD-011).</para>
/// </remarks>
public sealed class XpoCustomerQueryService(
    IObjectSpaceProvider objectSpaceProvider,
    ILogger<XpoCustomerQueryService> logger) : ICustomerQueryService
{
    /// <summary>
    /// Server cap so the endpoint never streams the whole customer master
    /// table in one request. The frontend asks for at most 50 today; this
    /// guard exists for ad-hoc API consumers.
    /// </summary>
    private const int MaxPageSize = 200;

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
        var (user, os) = ResolveUser(principal);
        if (user is null || os is null)
        {
            return Task.FromResult(Empty());
        }

        try
        {
            var session = ((XPObjectSpace)os).Session;
            var criteria = BuildCriteria(query);

            var totalCount = os.GetObjectsCount(typeof(Asiakas), criteria);
            if (totalCount == 0)
            {
                return Task.FromResult(Empty());
            }

            // Two pages: capped server-side via MaxPageSize, defaulted to 50
            // so the dx-list virtual scroll can ask for one screen at a time.
            const int defaultPageSize = 50;
            var pageSize = Math.Clamp(defaultPageSize, 1, MaxPageSize);

            // Production data has dangling FK targets (AsiakasLuottokysely2,
            // HakemusAsumisAika) that XPO cannot stub past — XPCollection's
            // delayed-association load throws CannotLoadObjectsException for
            // any page that contains such a row, which is most of them.
            //
            // Workaround: pull the AsiakasNumero list via Session.SelectData
            // (a key-only SELECT with SQL-level skip/top that does NOT touch
            // associations), then hydrate each row with GetObjectByKey
            // wrapped in try/catch so individual broken rows are skipped
            // instead of blanking the whole page.
            var skip = 0;
            var pageNumbers = LoadAsiakasNumberPage(session, criteria, skip, pageSize);
            if (pageNumbers.Count == 0)
            {
                return Task.FromResult(new CustomersResponseDto(Array.Empty<CustomerDto>(), totalCount));
            }

            var pageRows = new List<Asiakas>(pageNumbers.Count);
            var skippedDueToBrokenFk = 0;
            foreach (var num in pageNumbers)
            {
                try
                {
                    var row = session.GetObjectByKey<Asiakas>(num);
                    if (row is not null)
                    {
                        pageRows.Add(row);
                    }
                }
                catch (DevExpress.Xpo.Exceptions.CannotLoadObjectsException)
                {
                    skippedDueToBrokenFk++;
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex,
                        "Failed to load Asiakas {AsiakasNumero}; skipping in customers page",
                        num);
                }
            }
            if (skippedDueToBrokenFk > 0)
            {
                logger.LogInformation(
                    "Skipped {Count} Asiakas rows with broken FK references in customers page",
                    skippedDueToBrokenFk);
            }
            if (pageRows.Count == 0)
            {
                return Task.FromResult(new CustomersResponseDto(Array.Empty<CustomerDto>(), totalCount));
            }

            var asiakasNumbers = pageRows
                .Select(a => a.AsiakasNumero)
                .Where(n => n != 0)
                .Distinct()
                .Cast<object>()
                .ToArray();

            var counts = LookupCounts(session, asiakasNumbers);

            var items = new List<CustomerDto>(pageRows.Count);
            foreach (var row in pageRows)
            {
                try
                {
                    items.Add(MapDto(row, counts));
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex,
                        "Failed to map Asiakas {AsiakasNumero}; skipping in response",
                        row.AsiakasNumero);
                }
            }

            // The DB sort on SukuNimi is byte-level and only orders by the
            // first surname token; we re-sort the page in fi-FI collation
            // by the canonical DisplayName the wire exposes ("Sukunimi,
            // Etunimi" for persons, raw company name for Yritys).
            items.Sort(ByDisplayNameFi);

            return Task.FromResult(new CustomersResponseDto(items, totalCount));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Customers query failed for user {Email}", user.UserName);
            return Task.FromResult(Empty());
        }
        finally
        {
            os.Dispose();
        }
    }

    // -- criteria ---------------------------------------------------------

    private static CriteriaOperator? BuildCriteria(CustomerQueryParameters query)
    {
        var operands = new List<CriteriaOperator>();

        // Type discriminator. OnHenkilo / OnYritys are aliases; Yhteyshenkilo
        // is "neither", which we encode as the conjunction of both flags
        // being false. The aliases compare ObjectType.TypeName so XPO can
        // translate them to a CASE expression in SQL.
        switch (query.Type)
        {
            case CustomerTypes.Person:
                operands.Add(new BinaryOperator("OnHenkilo", true, BinaryOperatorType.Equal));
                break;
            case CustomerTypes.Company:
                operands.Add(new BinaryOperator("OnYritys", true, BinaryOperatorType.Equal));
                break;
            case CustomerTypes.ContactPerson:
                operands.Add(new BinaryOperator("OnHenkilo", false, BinaryOperatorType.Equal));
                operands.Add(new BinaryOperator("OnYritys", false, BinaryOperatorType.Equal));
                break;
        }

        if (!string.IsNullOrWhiteSpace(query.City))
        {
            operands.Add(new BinaryOperator("PostiToimiPaikka", query.City, BinaryOperatorType.Equal));
        }

        var search = query.Search?.Trim();
        if (!string.IsNullOrEmpty(search))
        {
            // SukuNimi is the canonical surname field on the Asiakas base
            // — Yritys.Yritysnimi and Yhteyshenkilo.Sukunimi are XPO
            // aliases over the same column, so a single SukuNimi LIKE
            // covers all three concrete types. Asiakas.Name is sometimes
            // empty in production and not safe to lean on.
            //
            // PII fields (Henkilo.PersonID / SSN, DOB, Age) are deliberately
            // excluded from the search surface.
            operands.Add(CriteriaOperator.Or(
                ContainsString("SukuNimi", search),
                ContainsString("KatuOsoite", search),
                ContainsString("PostiToimiPaikka", search),
                ContainsString("Email", search),
                ContainsString("Gsm", search),
                ContainsString("Puhelin", search)));
        }

        return operands.Count == 0
            ? null
            : operands.Count == 1
                ? operands[0]
                : CriteriaOperator.And(operands);
    }

    /// <summary>
    /// Builds a case-insensitive substring match on a string column.
    /// <see cref="ContainsOperator"/> is for collection containment
    /// (<c>SomeAssoc.Contains(x)</c>), not string LIKE — the visitor
    /// rejects scalar fields with "a reference property or collection
    /// association is expected". For string LIKE we go through the
    /// FunctionOperator (Contains) form, which the SQL generator
    /// translates to <c>field LIKE '%value%'</c>.
    /// </summary>
    private static CriteriaOperator ContainsString(string field, string value) =>
        new FunctionOperator(FunctionOperatorType.Contains, new OperandProperty(field), value);

    /// <summary>
    /// Projects the paginated AsiakasNumero key list for the given
    /// criteria, ordered by SukuNimi. Goes through Session.SelectData
    /// so the SQL is a single key-only SELECT with SQL-level skip/top
    /// — no XPO entity load, no eager-loaded associations, so the
    /// broken-FK rows that would block XPCollection.ToList() do not
    /// affect this query.
    /// </summary>
    private static List<int> LoadAsiakasNumberPage(
        Session session, CriteriaOperator? criteria, int skip, int top)
    {
        var classInfo = session.GetClassInfo(typeof(Asiakas));
        var properties = new CriteriaOperatorCollection
        {
            new OperandProperty("AsiakasNumero"),
        };
        var sorting = new SortingCollection
        {
            new SortProperty("SukuNimi", DevExpress.Xpo.DB.SortingDirection.Ascending),
        };

        var rows = session.SelectData(
            classInfo, properties, criteria,
            selectDeleted: false,
            skipSelectedRecords: skip,
            topSelectedRecords: top,
            sorting: sorting);

        var numbers = new List<int>(rows.Count);
        foreach (var row in rows)
        {
            if (row.Length > 0 && row[0] is not null)
            {
                numbers.Add(Convert.ToInt32(row[0]));
            }
        }
        return numbers;
    }

    // -- counts -----------------------------------------------------------

    private readonly record struct Counts(
        int Applications, int Reservations, int Contracts, int Offers, int Showings);

    private static Dictionary<int, Counts> LookupCounts(Session session, object[] asiakasNumbers)
    {
        if (asiakasNumbers.Length == 0)
        {
            return new Dictionary<int, Counts>();
        }

        var contracts = LookupContractCounts(session, asiakasNumbers);
        var applications = LookupApplicationCounts(session, asiakasNumbers);
        var reservations = LookupReservationCounts(session, asiakasNumbers);
        var showings = LookupShowingCounts(session, asiakasNumbers);

        var keys = contracts.Keys
            .Concat(applications.Keys)
            .Concat(reservations.Keys)
            .Concat(showings.Keys)
            .Distinct();

        var result = new Dictionary<int, Counts>();
        foreach (var k in keys)
        {
            result[k] = new Counts(
                Applications: applications.GetValueOrDefault(k),
                Reservations: reservations.GetValueOrDefault(k),
                Contracts:    contracts.GetValueOrDefault(k),
                // Offers overlap SopimusVaraus and require status semantics
                // we do not yet model — see OD-011. Keep the wire shape
                // stable by reporting zero until then.
                Offers:       0,
                Showings:     showings.GetValueOrDefault(k));
        }
        return result;
    }

    private static Dictionary<int, int> LookupContractCounts(Session session, object[] asiakasNumbers)
    {
        // Active = Voimassa flag on the SopimusAsiakas link AND the linked
        // Sopimus has a future or null-equivalent ValidTo. The Voimassa
        // flag alone is enough for active-tenant detection; the date guard
        // would require sub-property criteria across the assoc, which XPO
        // can do but at the cost of a much heavier query plan.
        var criteria = CriteriaOperator.And(
            new InOperator("Asiakas.AsiakasNumero", asiakasNumbers),
            new BinaryOperator("Voimassa", true, BinaryOperatorType.Equal));

        using var collection = new XPCollection<XpoContractAsiakas>(session, criteria);
        return GroupCountByAsiakas(collection.Cast<XpoContractAsiakas>(),
            x => x.Asiakas?.AsiakasNumero);
    }

    private static Dictionary<int, int> LookupApplicationCounts(Session session, object[] asiakasNumbers)
    {
        var criteria = CriteriaOperator.And(
            new InOperator("Asiakas.AsiakasNumero", asiakasNumbers),
            new BinaryOperator("Voimassa", true, BinaryOperatorType.Equal));

        using var collection = new XPCollection<XpoApplicationAsiakas>(session, criteria);
        return GroupCountByAsiakas(collection.Cast<XpoApplicationAsiakas>(),
            x => x.Asiakas?.AsiakasNumero);
    }

    private static Dictionary<int, int> LookupReservationCounts(Session session, object[] asiakasNumbers)
    {
        var criteria = new InOperator("Asiakas.AsiakasNumero", asiakasNumbers);
        using var collection = new XPCollection<XpoReservation>(session, criteria);
        return GroupCountByAsiakas(collection.Cast<XpoReservation>(),
            x => x.Asiakas?.AsiakasNumero);
    }

    private static Dictionary<int, int> LookupShowingCounts(Session session, object[] asiakasNumbers)
    {
        // Only inspections that are still actionable for the customer:
        // not cancelled, not yet handled, scheduled now-or-later.
        var nowUtc = DateTime.UtcNow;
        var criteria = CriteriaOperator.And(
            new InOperator("Asiakas.AsiakasNumero", asiakasNumbers),
            new BinaryOperator("IsCancelled", false, BinaryOperatorType.Equal),
            new BinaryOperator("IsHandled",   false, BinaryOperatorType.Equal),
            new BinaryOperator("StartedOn",   nowUtc, BinaryOperatorType.GreaterOrEqual));

        using var collection = new XPCollection<XpoInspection>(session, criteria);
        return GroupCountByAsiakas(collection.Cast<XpoInspection>(),
            x => x.Asiakas?.AsiakasNumero);
    }

    private static Dictionary<int, int> GroupCountByAsiakas<T>(IEnumerable<T> rows, Func<T, int?> key)
    {
        var result = new Dictionary<int, int>();
        foreach (var r in rows)
        {
            var k = key(r);
            if (k is null || k.Value == 0) continue;
            result[k.Value] = result.GetValueOrDefault(k.Value) + 1;
        }
        return result;
    }

    // -- mapping ----------------------------------------------------------

    private static CustomerDto MapDto(Asiakas a, Dictionary<int, Counts> counts)
    {
        var snapshot = counts.GetValueOrDefault(a.AsiakasNumero);
        var (type, displayName) = DiscriminateAndName(a);
        var initials = ComputeInitials(a, type);

        return new CustomerDto(
            Id: a.AsiakasNumero.ToString(CultureInfo.InvariantCulture),
            Type: type,
            DisplayName: displayName,
            Initials: initials,
            Counts: new CustomerCountsDto(
                Applications: snapshot.Applications,
                Reservations: snapshot.Reservations,
                Contracts:    snapshot.Contracts,
                Offers:       snapshot.Offers,
                Showings:     snapshot.Showings),
            PrimaryAddress: NullIfBlank(a.KatuOsoite),
            City:           NullIfBlank(a.PostiToimiPaikka),
            Tag:            null,
            Phone:          NullIfBlank(a.Gsm) ?? NullIfBlank(a.Puhelin),
            Email:          NullIfBlank(a.Email),
            FirstName:       type == CustomerTypes.Person        ? ((Henkilo)a).EtuNimi
                          :  type == CustomerTypes.ContactPerson ? ((Yhteyshenkilo)a).EtuNimi
                          :  null,
            LastName:        type == CustomerTypes.Person        ? ((Henkilo)a).SukuNimi
                          :  type == CustomerTypes.ContactPerson ? ((Yhteyshenkilo)a).SukuNimi
                          :  null,
            CompanyName:     type == CustomerTypes.Company       ? ((Yritys)a).SukuNimi : null,
            BusinessId:      type == CustomerTypes.Company       ? NullIfBlank(((Yritys)a).CompanyID) : null,
            ParentCompanyId: type == CustomerTypes.ContactPerson
                                ? ((Yhteyshenkilo)a).Yritys?.AsiakasNumero.ToString(CultureInfo.InvariantCulture)
                                : null,
            ParentCompanyName: type == CustomerTypes.ContactPerson
                                ? NullIfBlank(((Yhteyshenkilo)a).Yritys?.SukuNimi)
                                : null);
    }

    private static (string Type, string DisplayName) DiscriminateAndName(Asiakas a) => a switch
    {
        Henkilo h        => (CustomerTypes.Person,        BuildPersonDisplayName(h.SukuNimi, h.EtuNimi, h.Name)),
        Yritys y         => (CustomerTypes.Company,       NullIfBlank(y.SukuNimi) ?? NullIfBlank(y.Name) ?? "(nimetön yritys)"),
        Yhteyshenkilo yh => (CustomerTypes.ContactPerson, BuildPersonDisplayName(yh.SukuNimi, yh.EtuNimi, yh.Name)),
        _                => (CustomerTypes.Person,        NullIfBlank(a.Name) ?? "(nimetön)"),
    };

    private static string BuildPersonDisplayName(string? lastName, string? firstName, string? fallback)
    {
        var ln = (lastName ?? string.Empty).Trim();
        var fn = (firstName ?? string.Empty).Trim();
        if (ln.Length > 0 && fn.Length > 0) return $"{ln}, {fn}";
        if (ln.Length > 0) return ln;
        if (fn.Length > 0) return fn;
        return NullIfBlank(fallback) ?? "(nimetön)";
    }

    private static string ComputeInitials(Asiakas a, string type)
    {
        return type switch
        {
            CustomerTypes.Person =>
                Letters(((Henkilo)a).SukuNimi, ((Henkilo)a).EtuNimi),
            CustomerTypes.ContactPerson =>
                Letters(((Yhteyshenkilo)a).SukuNimi, ((Yhteyshenkilo)a).EtuNimi),
            CustomerTypes.Company =>
                CompanyInitials(((Yritys)a).SukuNimi ?? a.Name),
            _ => "?",
        };
    }

    private static string Letters(string? lastName, string? firstName)
    {
        var ln = (lastName ?? string.Empty).Trim();
        var fn = (firstName ?? string.Empty).Trim();
        var l1 = ln.Length > 0 ? char.ToUpperInvariant(ln[0]).ToString() : string.Empty;
        var l2 = fn.Length > 0 ? char.ToUpperInvariant(fn[0]).ToString() : string.Empty;
        return string.Concat(l1, l2) is { Length: > 0 } combined ? combined : "?";
    }

    private static string CompanyInitials(string? name)
    {
        var raw = (name ?? string.Empty).Trim();
        if (raw.Length == 0) return "?";
        var words = raw.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (words.Length >= 2)
        {
            return string.Concat(
                char.ToUpperInvariant(words[0][0]),
                char.ToUpperInvariant(words[1][0]));
        }
        return raw.Length >= 2
            ? string.Concat(char.ToUpperInvariant(raw[0]), char.ToUpperInvariant(raw[1]))
            : char.ToUpperInvariant(raw[0]).ToString();
    }

    private static string? NullIfBlank(string? s) =>
        string.IsNullOrWhiteSpace(s) ? null : s.Trim();

    // -- helpers ----------------------------------------------------------

    private static CustomersResponseDto Empty() =>
        new(Array.Empty<CustomerDto>(), 0);

    private (xVasuSecuritySystemUser? user, IObjectSpace? os) ResolveUser(ClaimsPrincipal principal)
    {
        var os = objectSpaceProvider.CreateObjectSpace();

        var email = EmailResolver.ResolveEmail(principal);
        if (string.IsNullOrEmpty(email))
        {
            logger.LogWarning("Customers request rejected — principal had no resolvable email");
            os.Dispose();
            return (null, null);
        }

        var variants = EmailResolver.BuildEmailVariants(email);
        var resolved = os.FindObject<xVasuSecuritySystemUser>(EmailResolver.BuildUserCriteria(variants, email));
        if (resolved is null)
        {
            logger.LogInformation("Customers request rejected — user {Email} not provisioned", email);
            os.Dispose();
            return (null, null);
        }

        return (resolved, os);
    }
}
