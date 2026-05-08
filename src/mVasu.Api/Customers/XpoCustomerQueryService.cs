using System.Diagnostics;
using System.Globalization;
using System.Security.Claims;
using DevExpress.Data.Filtering;
using DevExpress.ExpressApp;
using DevExpress.ExpressApp.Xpo;
using DevExpress.Xpo;
using DevExpress.Xpo.DB;
using mVasu.Api.Authentication;
using mVasu.Api.Common;
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
    CustomerFieldAccessPolicy fieldAccessPolicy,
    ILogger<XpoCustomerQueryService> logger) : ICustomerQueryService
{
    /// <summary>
    /// Server cap so the endpoint never streams the whole customer master
    /// table in one request. The frontend asks for at most 50 today; this
    /// guard exists for ad-hoc API consumers.
    /// </summary>
    private const int MaxPageSize = 200;

    /// <summary>
    /// Hard ceiling on the FTS pass. SQL Server's CONTAINS is fast even
    /// against large fulltext catalogs, but we'd still feed every
    /// returned AsiakasNumero into a downstream <c>InOperator</c> — and
    /// XPO's IN-list serializer chokes once the array runs into the
    /// thousands. 1000 is well past the point a human can scan and far
    /// below SQL's IN-list limit.
    /// </summary>
    private const int FtsResultCap = 1000;

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
        var (user, os, access) = ResolveUser(principal);
        if (user is null || os is null)
        {
            return Task.FromResult(Empty());
        }

        try
        {
            var session = ((XPObjectSpace)os).Session;
            var hasSearch = !string.IsNullOrWhiteSpace(query.Search);

            // Phase timings — single Information line at the bottom shows
            // where the wall-clock went. Cheap (Stopwatch is monotonic
            // long ticks); flip the log level if it ever becomes noisy.
            var sw = Stopwatch.StartNew();
            long tFts = 0, tCount = 0, tPage = 0, tBase = 0, tSubtype = 0, tCounts = 0, tMap = 0;

            // Search path: SQL Server FULLTEXT via CONTAINS against the
            // existing fts_Asiakas catalog (EtuNimi, SukuNimi, KatuOsoite,
            // Email, Gsm — Finnish word breaker). The FTS query returns
            // a list of matching AsiakasNumero ids that we AND-in to the
            // type/city criteria via InOperator, so the rest of the flow
            // (counts, pagination, projections) stays criteria-driven.
            //
            // The previous prefix/substring hybrid is gone — FTS is
            // strictly faster and gives stem matching (e.g. "koivu" hits
            // "Koivunen", "Koivulan", "Koiv u") plus EtuNimi search,
            // neither of which the LIKE-based path supported.
            CriteriaOperator? activeCriteria = BuildFilterCriteria(query);
            if (hasSearch)
            {
                var t0 = sw.ElapsedMilliseconds;
                var ftsIds = LoadFtsAsiakasNumbers(session, query.Search!);
                tFts = sw.ElapsedMilliseconds - t0;

                if (ftsIds.Count == 0)
                {
                    return Task.FromResult(Empty());
                }

                var idCriteria = new InOperator(
                    "AsiakasNumero", ftsIds.Cast<object>().ToArray());
                activeCriteria = activeCriteria is null
                    ? idCriteria
                    : CriteriaOperator.And(activeCriteria, idCriteria);

                logger.LogDebug(
                    "Customer FTS '{Query}' matched {Count} ids (capped at {Cap})",
                    query.Search, ftsIds.Count, FtsResultCap);
            }

            var tCountStart = sw.ElapsedMilliseconds;
            var totalCount = os.GetObjectsCount(typeof(Asiakas), activeCriteria);
            tCount = sw.ElapsedMilliseconds - tCountStart;

            if (totalCount == 0)
            {
                return Task.FromResult(Empty());
            }

            // Two pages: capped server-side via MaxPageSize, defaulted to 50
            // so the dx-list virtual scroll can ask for one screen at a time.
            const int defaultPageSize = 50;
            var pageSize = Math.Clamp(defaultPageSize, 1, MaxPageSize);

            // Production data has dangling FK targets (AsiakasLuottokysely2,
            // HakemusAsumisAika) that XPO cannot stub past — any attempt to
            // hydrate the Asiakas entity (XPCollection.ToList(), or even
            // GetObjectByKey for an individual row) eager-loads delayed
            // associations and throws CannotLoadObjectsException when one
            // of those FK targets is missing.
            //
            // Workaround: project every wire-DTO field via Session.SelectData
            // — base-class fields from Asiakas, then per-subtype fields
            // from Henkilo / Yritys / Yhteyshenkilo. Nothing actually loads
            // an Asiakas entity, so missing FKs on Henkilo.LastOne and
            // friends are inert.
            var tPageStart = sw.ElapsedMilliseconds;
            var pageNumbers = LoadAsiakasNumberPage(session, activeCriteria, skip: 0, top: pageSize);
            tPage = sw.ElapsedMilliseconds - tPageStart;

            if (pageNumbers.Count == 0)
            {
                return Task.FromResult(new CustomersResponseDto(Array.Empty<CustomerDto>(), totalCount));
            }

            var idArgs = pageNumbers.Cast<object>().ToArray();

            var tBaseStart = sw.ElapsedMilliseconds;
            var baseRows = LoadAsiakasBaseRows(session, idArgs);
            tBase = sw.ElapsedMilliseconds - tBaseStart;

            var tSubtypeStart = sw.ElapsedMilliseconds;
            var personRows  = LoadHenkiloRows(session, idArgs);
            var companyRows = LoadYritysRows(session, idArgs);
            var contactRows = LoadYhteyshenkiloRows(session, idArgs);
            tSubtype = sw.ElapsedMilliseconds - tSubtypeStart;

            var tCountsStart = sw.ElapsedMilliseconds;
            var counts = LookupCounts(session, idArgs);
            tCounts = sw.ElapsedMilliseconds - tCountsStart;

            var tMapStart = sw.ElapsedMilliseconds;

            var items = new List<CustomerDto>(pageNumbers.Count);
            foreach (var num in pageNumbers)
            {
                if (!baseRows.TryGetValue(num, out var baseRow))
                {
                    // The page id list came from the same criteria; if the
                    // base row is missing here it means PermissionPolicy or
                    // a deletion raced us — skip rather than 500.
                    continue;
                }

                CustomerDto? dto = null;
                try
                {
                    dto = baseRow.Type switch
                    {
                        CustomerTypes.Person        when personRows.TryGetValue(num, out var p) => MapPerson(num, baseRow, p, counts, access),
                        CustomerTypes.Company       when companyRows.TryGetValue(num, out var c) => MapCompany(num, baseRow, c, counts, access),
                        CustomerTypes.ContactPerson when contactRows.TryGetValue(num, out var y) => MapContact(num, baseRow, y, counts, access),
                        _ => MapFallback(num, baseRow, counts, access),
                    };
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex,
                        "Failed to map Asiakas {AsiakasNumero}; skipping in response",
                        num);
                }

                if (dto is not null)
                {
                    items.Add(dto);
                }
            }

            // The DB sort on SukuNimi is byte-level and only orders by the
            // first surname token; we re-sort the page in fi-FI collation
            // by the canonical DisplayName the wire exposes ("Sukunimi,
            // Etunimi" for persons, raw company name for Yritys).
            items.Sort(ByDisplayNameFi);
            tMap = sw.ElapsedMilliseconds - tMapStart;

            logger.LogInformation(
                "Customers query: total={Total}ms (fts={Fts} count={Count} page={Page} base={Base} subtype={Subtype} counts={Counts} map={Map}) — items={Items} hasSearch={HasSearch}",
                sw.ElapsedMilliseconds, tFts, tCount, tPage, tBase, tSubtype, tCounts, tMap,
                items.Count, hasSearch);

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

    public Task<CustomerDto?> GetAsync(
        ClaimsPrincipal principal,
        int asiakasNumero,
        CancellationToken cancellationToken = default)
    {
        var (user, os, access) = ResolveUser(principal);
        if (user is null || os is null)
        {
            return Task.FromResult<CustomerDto?>(null);
        }

        try
        {
            var session = ((XPObjectSpace)os).Session;
            var ids = new object[] { asiakasNumero };

            // Same row-projection strategy as ListAsync: never hydrate an
            // Asiakas entity, so dangling FK references on Henkilo.LastOne
            // and friends can't take the response down.
            var baseRows    = LoadAsiakasBaseRows(session, ids);
            if (!baseRows.TryGetValue(asiakasNumero, out var baseRow))
            {
                // No row visible to this user (either deleted or filtered
                // by PermissionPolicy). Return 404 to the caller.
                return Task.FromResult<CustomerDto?>(null);
            }

            var personRows  = LoadHenkiloRows(session, ids);
            var companyRows = LoadYritysRows(session, ids);
            var contactRows = LoadYhteyshenkiloRows(session, ids);
            var counts      = LookupCounts(session, ids);

            CustomerDto dto = baseRow.Type switch
            {
                CustomerTypes.Person        when personRows.TryGetValue(asiakasNumero, out var p) => MapPerson(asiakasNumero, baseRow, p, counts, access),
                CustomerTypes.Company       when companyRows.TryGetValue(asiakasNumero, out var c) => MapCompany(asiakasNumero, baseRow, c, counts, access),
                CustomerTypes.ContactPerson when contactRows.TryGetValue(asiakasNumero, out var y) => MapContact(asiakasNumero, baseRow, y, counts, access),
                _ => MapFallback(asiakasNumero, baseRow, counts, access),
            };

            return Task.FromResult<CustomerDto?>(dto);
        }
        catch (Exception ex)
        {
            logger.LogError(ex,
                "Customer detail load failed for AsiakasNumero {AsiakasNumero}, user {Email}",
                asiakasNumero, user.UserName);
            return Task.FromResult<CustomerDto?>(null);
        }
        finally
        {
            os.Dispose();
        }
    }

    // -- criteria ---------------------------------------------------------

    /// <summary>
    /// Builds the non-search filter criteria (type discriminator + city).
    /// The free-text search predicate is intentionally NOT included here
    /// — search runs through SQL Server FULLTEXT (see
    /// <see cref="LoadFtsAsiakasNumbers"/>) and the matching ids are
    /// AND-ed in via <see cref="InOperator"/>. Splitting it this way
    /// lets the GetObjectsCount / LoadAsiakasNumberPage path stay
    /// criteria-driven without re-implementing FTS in XPO terms.
    /// </summary>
    private static CriteriaOperator? BuildFilterCriteria(CustomerQueryParameters query)
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

        return operands.Count == 0
            ? null
            : operands.Count == 1
                ? operands[0]
                : CriteriaOperator.And(operands);
    }

    // -- fulltext search --------------------------------------------------

    /// <summary>
    /// Runs SQL Server <c>CONTAINS</c> against the existing
    /// <c>fts_Asiakas</c> fulltext catalog and returns matching
    /// <c>AsiakasNumero</c> values. The catalog covers EtuNimi,
    /// SukuNimi, KatuOsoite, Email and Gsm in Finnish (LCID 1053), so
    /// per-word stemming and inflection are handled by the engine.
    /// Puhelin and PostiToimiPaikka are NOT indexed; phone searches go
    /// through Gsm, and PostiToimiPaikka is exposed via the dedicated
    /// city dropdown filter.
    /// </summary>
    /// <remarks>
    /// Capped at <see cref="FtsResultCap"/> ids — once an FTS pass
    /// returns more than that, the dx-list view can't render them
    /// usefully anyway and the InOperator that consumes the list grows
    /// past XPO's serializer comfort zone. The user is expected to
    /// refine the search.
    /// </remarks>
    private List<int> LoadFtsAsiakasNumbers(Session session, string searchTerm)
    {
        var ftsExpression = FtsExpressionBuilder.Build(searchTerm);
        if (ftsExpression is null)
        {
            return new List<int>();
        }

        // Resolve the physical table name through the XPO dictionary so
        // we don't hardcode `t_Asiakas` — the legacy schema applies a
        // `t_` prefix at the dictionary level, but renaming or
        // un-prefixing the table elsewhere shouldn't quietly skip the
        // FTS path.
        var classInfo = session.GetClassInfo(typeof(Asiakas));
        var qualifiedTable = XpoTableNameResolver.Qualified(classInfo.TableName);

        var sql =
            $"SELECT TOP {FtsResultCap} [AsiakasNumero] " +
            $"FROM {qualifiedTable} " +
            "WHERE [GCRecord] IS NULL " +
            "  AND CONTAINS(([EtuNimi], [SukuNimi], [KatuOsoite], [Email], [Gsm]), @ftsTerm)";

        SelectedData data;
        try
        {
            data = session.ExecuteQuery(sql,
                new[] { "@ftsTerm" },
                new object[] { ftsExpression });
        }
        catch (Exception ex)
        {
            // FTS catalog disabled or missing in some environment? Bail
            // safely — empty list yields zero results, the UI shows the
            // standard "no matches" empty state. Logged at warning so
            // ops can spot a regressed catalog without flooding logs.
            logger.LogWarning(ex,
                "FTS query failed for term {Term} — returning empty match list",
                searchTerm);
            return new List<int>();
        }

        var ids = new List<int>();
        if (data.ResultSet.Length == 0) return ids;

        foreach (var row in data.ResultSet[0].Rows)
        {
            if (row.Values.Length > 0 && row.Values[0] is not null)
            {
                ids.Add(Convert.ToInt32(row.Values[0]));
            }
        }
        return ids;
    }

    // FTS expression construction lives in mVasu.Api.Common.FtsExpressionBuilder
    // — the same helper is shared by Tiskilista's FTS pre-pass.

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

    private Dictionary<int, Counts> LookupCounts(Session session, object[] asiakasNumbers)
    {
        if (asiakasNumbers.Length == 0)
        {
            return new Dictionary<int, Counts>();
        }

        // Each lookup is independently wrapped — if one source has a
        // broken-FK row in the page's range we log it and return zero
        // for that dimension instead of failing the whole response.
        var contracts    = SafeLookupCount("contract",     () => LookupContractCounts(session, asiakasNumbers));
        var applications = SafeLookupCount("application",  () => LookupApplicationCounts(session, asiakasNumbers));
        var reservations = SafeLookupCount("reservation",  () => LookupReservationCounts(session, asiakasNumbers));
        var showings     = SafeLookupCount("showing",      () => LookupShowingCounts(session, asiakasNumbers));

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

    private Dictionary<int, int> SafeLookupCount(string label, Func<Dictionary<int, int>> source)
    {
        try
        {
            return source();
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex,
                "Customers count lookup failed for {Source}; treating as zero",
                label);
            return new Dictionary<int, int>();
        }
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
        return ProjectAsiakasNumeroCounts(session, typeof(XpoContractAsiakas), criteria);
    }

    private static Dictionary<int, int> LookupApplicationCounts(Session session, object[] asiakasNumbers)
    {
        var criteria = CriteriaOperator.And(
            new InOperator("Asiakas.AsiakasNumero", asiakasNumbers),
            new BinaryOperator("Voimassa", true, BinaryOperatorType.Equal));
        return ProjectAsiakasNumeroCounts(session, typeof(XpoApplicationAsiakas), criteria);
    }

    private static Dictionary<int, int> LookupReservationCounts(Session session, object[] asiakasNumbers)
    {
        var criteria = new InOperator("Asiakas.AsiakasNumero", asiakasNumbers);
        return ProjectAsiakasNumeroCounts(session, typeof(XpoReservation), criteria);
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
        return ProjectAsiakasNumeroCounts(session, typeof(XpoInspection), criteria);
    }

    /// <summary>
    /// Counts rows of <paramref name="linkType"/> grouped by
    /// <c>Asiakas.AsiakasNumero</c> using a key-only
    /// <see cref="Session.SelectData"/> projection. This avoids the
    /// previous <c>XPCollection</c> path which loaded every column of
    /// every link row plus eager-fetched the full Asiakas entity to
    /// access its key — both wasteful when all we need is the integer
    /// to bin on.
    /// </summary>
    /// <remarks>
    /// The generated SQL is <c>SELECT [Asiakas].[AsiakasNumero] FROM
    /// [link] INNER JOIN [t_Asiakas] ... WHERE ... </c>. Grouping
    /// happens in-memory, which is fine because the result set is
    /// scoped to the page's 50 ids — even one row per inspection per
    /// active customer is tens or low hundreds.
    /// </remarks>
    private static Dictionary<int, int> ProjectAsiakasNumeroCounts(
        Session session, Type linkType, CriteriaOperator criteria)
    {
        var classInfo = session.GetClassInfo(linkType);
        var props = new CriteriaOperatorCollection
        {
            new OperandProperty("Asiakas.AsiakasNumero"),
        };

        var rows = session.SelectData(
            classInfo, props, criteria,
            selectDeleted: false,
            topSelectedRecords: int.MaxValue,
            sorting: new SortingCollection());

        var result = new Dictionary<int, int>();
        foreach (var row in rows)
        {
            if (row.Length == 0 || row[0] is null) continue;
            var key = Convert.ToInt32(row[0]);
            if (key == 0) continue;
            result[key] = result.GetValueOrDefault(key) + 1;
        }
        return result;
    }

    // -- row projections --------------------------------------------------

    /// <summary>
    /// Asiakas base-class fields projected into a flat record that's safe
    /// to read without ever touching a delayed association. The Type
    /// discriminator is derived from <c>OnHenkilo</c>/<c>OnYritys</c>
    /// alias columns so we don't need <c>ObjectType.TypeName</c> string
    /// matching on the wire.
    /// </summary>
    private readonly record struct AsiakasBaseRow(
        string Type,
        string? SukuNimi,
        string? KatuOsoite,
        string? PostiNumero,
        string? PostiToimiPaikka,
        string? Email,
        string? Gsm,
        string? Puhelin,
        string? Maa,
        string? LangCode,
        string? ToimiAla,
        string? TyoPaikka,
        int? BruttoTulot,
        bool? EmailKayttoSallittu,
        bool? PuhNoKayttoSallittu,
        bool? Suoramarkkinointikielto);
        // NOTE: Asiakas.InfoMessage is XPO-non-persistent (computed from
        // related ASMA flags) — SelectData rejects it. If we want the
        // banner back, derive it on the fly from the relations rather
        // than projecting it as a column.

    private readonly record struct PersonRow(string? EtuNimi, string? SukuNimi, string? Ammatti);
    private readonly record struct CompanyRow(string? CompanyID, string? Name);
    private readonly record struct ContactRow(
        string? EtuNimi,
        string? SukuNimi,
        int? ParentAsiakasNumero,
        string? ParentCompanyName);

    private static Dictionary<int, AsiakasBaseRow> LoadAsiakasBaseRows(Session session, object[] asiakasNumbers)
    {
        var classInfo = session.GetClassInfo(typeof(Asiakas));
        var props = new CriteriaOperatorCollection
        {
            new OperandProperty("AsiakasNumero"),
            new OperandProperty("OnHenkilo"),
            new OperandProperty("OnYritys"),
            new OperandProperty("SukuNimi"),
            new OperandProperty("KatuOsoite"),
            new OperandProperty("PostiNumero"),
            new OperandProperty("PostiToimiPaikka"),
            new OperandProperty("Email"),
            new OperandProperty("Gsm"),
            new OperandProperty("Puhelin"),
            new OperandProperty("Maa"),
            new OperandProperty("LangCode"),
            new OperandProperty("ToimiAla"),
            new OperandProperty("TyoPaikka"),
            new OperandProperty("BruttoTulot"),
            new OperandProperty("EmailKayttoSallittu"),
            new OperandProperty("PuhNoKayttoSallittu"),
            new OperandProperty("Suoramarkkinointikielto"),
        };
        var criteria = new InOperator("AsiakasNumero", asiakasNumbers);
        var rows = session.SelectData(
            classInfo, props, criteria,
            selectDeleted: false,
            topSelectedRecords: int.MaxValue,
            sorting: new SortingCollection());

        var result = new Dictionary<int, AsiakasBaseRow>(rows.Count);
        foreach (var row in rows)
        {
            var num = ToInt(row[0]);
            if (num is null) continue;

            var onHenkilo = ToBoolish(row[1]);
            var onYritys  = ToBoolish(row[2]);
            var type = onHenkilo ? CustomerTypes.Person
                     : onYritys  ? CustomerTypes.Company
                     : CustomerTypes.ContactPerson;

            result[num.Value] = new AsiakasBaseRow(
                Type: type,
                SukuNimi:               row[3]  as string,
                KatuOsoite:             row[4]  as string,
                PostiNumero:            row[5]  as string,
                PostiToimiPaikka:       row[6]  as string,
                Email:                  row[7]  as string,
                Gsm:                    row[8]  as string,
                Puhelin:                row[9]  as string,
                Maa:                    row[10] as string,
                LangCode:               row[11] as string,
                ToimiAla:               row[12] as string,
                TyoPaikka:              row[13] as string,
                BruttoTulot:            ToInt(row[14]),
                EmailKayttoSallittu:    ToBool(row[15]),
                PuhNoKayttoSallittu:    ToBool(row[16]),
                Suoramarkkinointikielto: ToBool(row[17]));
        }
        return result;
    }

    private static Dictionary<int, PersonRow> LoadHenkiloRows(Session session, object[] asiakasNumbers)
    {
        var classInfo = session.GetClassInfo(typeof(Henkilo));
        var props = new CriteriaOperatorCollection
        {
            new OperandProperty("AsiakasNumero"),
            new OperandProperty("EtuNimi"),
            new OperandProperty("SukuNimi"),
            new OperandProperty("Ammatti"),
        };
        var rows = session.SelectData(
            classInfo, props,
            new InOperator("AsiakasNumero", asiakasNumbers),
            selectDeleted: false,
            topSelectedRecords: int.MaxValue,
            sorting: new SortingCollection());

        var result = new Dictionary<int, PersonRow>(rows.Count);
        foreach (var row in rows)
        {
            var num = ToInt(row[0]);
            if (num is null) continue;
            result[num.Value] = new PersonRow(
                EtuNimi:  row[1] as string,
                SukuNimi: row[2] as string,
                Ammatti:  row[3] as string);
        }
        return result;
    }

    private static Dictionary<int, CompanyRow> LoadYritysRows(Session session, object[] asiakasNumbers)
    {
        var classInfo = session.GetClassInfo(typeof(Yritys));
        var props = new CriteriaOperatorCollection
        {
            new OperandProperty("AsiakasNumero"),
            new OperandProperty("CompanyID"),
            new OperandProperty("SukuNimi"),
        };
        var rows = session.SelectData(
            classInfo, props,
            new InOperator("AsiakasNumero", asiakasNumbers),
            selectDeleted: false,
            topSelectedRecords: int.MaxValue,
            sorting: new SortingCollection());

        var result = new Dictionary<int, CompanyRow>(rows.Count);
        foreach (var row in rows)
        {
            var num = ToInt(row[0]);
            if (num is null) continue;
            // Yritysnimi is an XPO alias over SukuNimi — the underlying
            // column is the same, so we read SukuNimi here.
            result[num.Value] = new CompanyRow(row[1] as string, row[2] as string);
        }
        return result;
    }

    private static Dictionary<int, ContactRow> LoadYhteyshenkiloRows(Session session, object[] asiakasNumbers)
    {
        var classInfo = session.GetClassInfo(typeof(Yhteyshenkilo));
        var props = new CriteriaOperatorCollection
        {
            new OperandProperty("AsiakasNumero"),
            new OperandProperty("EtuNimi"),
            new OperandProperty("SukuNimi"),
            // Navigation-property projection — XPO emits a LEFT JOIN so a
            // dangling Yritys FK comes back as null instead of throwing.
            new OperandProperty("Yritys.AsiakasNumero"),
            new OperandProperty("Yritys.SukuNimi"),
        };
        var rows = session.SelectData(
            classInfo, props,
            new InOperator("AsiakasNumero", asiakasNumbers),
            selectDeleted: false,
            topSelectedRecords: int.MaxValue,
            sorting: new SortingCollection());

        var result = new Dictionary<int, ContactRow>(rows.Count);
        foreach (var row in rows)
        {
            var num = ToInt(row[0]);
            if (num is null) continue;
            result[num.Value] = new ContactRow(
                EtuNimi:             row[1] as string,
                SukuNimi:            row[2] as string,
                ParentAsiakasNumero: ToInt(row[3]),
                ParentCompanyName:   row[4] as string);
        }
        return result;
    }

    // -- mapping ----------------------------------------------------------

    private static CustomerDto MapPerson(int asiakasNumero, AsiakasBaseRow b, PersonRow p,
                                          Dictionary<int, Counts> counts,
                                          CustomerFieldAccessSnapshot access)
    {
        var clrType = typeof(Henkilo);
        var lastName = NullIfBlank(Allow(access, clrType, "SukuNimi", p.SukuNimi))
                       ?? NullIfBlank(Allow(access, typeof(Asiakas), "SukuNimi", b.SukuNimi));
        var firstName = NullIfBlank(Allow(access, clrType, "EtuNimi", p.EtuNimi));
        return BuildPersonDto(asiakasNumero, CustomerTypes.Person, b, counts, access, clrType,
            firstName: firstName, lastName: lastName,
            profession: NullIfBlank(Allow(access, clrType, "Ammatti", p.Ammatti)),
            parentCompanyId: null, parentCompanyName: null);
    }

    private static CustomerDto MapContact(int asiakasNumero, AsiakasBaseRow b, ContactRow y,
                                           Dictionary<int, Counts> counts,
                                           CustomerFieldAccessSnapshot access)
    {
        var clrType = typeof(Yhteyshenkilo);
        var lastName = NullIfBlank(Allow(access, clrType, "SukuNimi", y.SukuNimi))
                       ?? NullIfBlank(Allow(access, typeof(Asiakas), "SukuNimi", b.SukuNimi));
        var firstName = NullIfBlank(Allow(access, clrType, "EtuNimi", y.EtuNimi));
        return BuildPersonDto(asiakasNumero, CustomerTypes.ContactPerson, b, counts, access, clrType,
            firstName: firstName, lastName: lastName,
            profession: null,
            parentCompanyId: y.ParentAsiakasNumero?.ToString(CultureInfo.InvariantCulture),
            parentCompanyName: NullIfBlank(y.ParentCompanyName));
    }

    private static CustomerDto MapCompany(int asiakasNumero, AsiakasBaseRow b, CompanyRow c,
                                           Dictionary<int, Counts> counts,
                                           CustomerFieldAccessSnapshot access)
    {
        var snapshot = counts.GetValueOrDefault(asiakasNumero);
        var clrType = typeof(Yritys);

        // SukuNimi / Yritysnimi share the underlying column. The display
        // name needs *some* readable token to avoid degenerate output;
        // when SukuNimi is denied we still surface "(nimetön yritys)" so
        // the row is identifiable in the list.
        var allowedSukuName = Allow(access, clrType, "SukuNimi", c.Name)
                              ?? Allow(access, typeof(Asiakas), "SukuNimi", b.SukuNimi);
        var displayName = NullIfBlank(allowedSukuName) ?? "(nimetön yritys)";

        return new CustomerDto(
            Id: asiakasNumero.ToString(CultureInfo.InvariantCulture),
            Type: CustomerTypes.Company,
            DisplayName: displayName,
            Initials: CompanyInitials(displayName),
            Counts: new CustomerCountsDto(
                snapshot.Applications, snapshot.Reservations,
                snapshot.Contracts, snapshot.Offers, snapshot.Showings),
            PrimaryAddress: NullIfBlank(Allow(access, typeof(Asiakas), "KatuOsoite", b.KatuOsoite)),
            City:           NullIfBlank(Allow(access, typeof(Asiakas), "PostiToimiPaikka", b.PostiToimiPaikka)),
            Tag:            null,
            Phone:          NullIfBlank(Allow(access, typeof(Asiakas), "Gsm",     b.Gsm))
                            ?? NullIfBlank(Allow(access, typeof(Asiakas), "Puhelin", b.Puhelin)),
            Email:          NullIfBlank(Allow(access, typeof(Asiakas), "Email", b.Email)),
            FirstName:       null,
            LastName:        null,
            CompanyName:     displayName,
            BusinessId:      NullIfBlank(Allow(access, clrType, "CompanyID", c.CompanyID)),
            ParentCompanyId: null,
            ParentCompanyName: null,
            PostalCode:      NullIfBlank(Allow(access, typeof(Asiakas), "PostiNumero", b.PostiNumero)),
            Country:         NullIfBlank(Allow(access, typeof(Asiakas), "Maa",         b.Maa)),
            Language:        NullIfBlank(Allow(access, typeof(Asiakas), "LangCode",    b.LangCode)),
            Profession:      null,
            Industry:        NullIfBlank(Allow(access, typeof(Asiakas), "ToimiAla", b.ToimiAla)),
            Workplace:       null,
            Income:          Allow(access, typeof(Asiakas), "BruttoTulot",            b.BruttoTulot),
            EmailMarketingAllowed:    Allow(access, typeof(Asiakas), "EmailKayttoSallittu",     b.EmailKayttoSallittu),
            PhoneMarketingAllowed:    Allow(access, typeof(Asiakas), "PuhNoKayttoSallittu",     b.PuhNoKayttoSallittu),
            DirectMarketingForbidden: Allow(access, typeof(Asiakas), "Suoramarkkinointikielto", b.Suoramarkkinointikielto));
    }

    /// <summary>
    /// Last-resort mapping when the per-subtype row is missing — we fall
    /// back to whatever the Asiakas base row has. Mostly defensive: the
    /// page numbers came from the same query, so the subtype row should
    /// be present.
    /// </summary>
    private static CustomerDto MapFallback(int asiakasNumero, AsiakasBaseRow b,
                                            Dictionary<int, Counts> counts,
                                            CustomerFieldAccessSnapshot access)
    {
        var clrType = b.Type switch
        {
            CustomerTypes.Person        => typeof(Henkilo),
            CustomerTypes.Company       => typeof(Yritys),
            CustomerTypes.ContactPerson => typeof(Yhteyshenkilo),
            _                           => typeof(Asiakas),
        };
        return BuildPersonDto(asiakasNumero, b.Type, b, counts, access, clrType,
            firstName: null,
            lastName:  NullIfBlank(Allow(access, typeof(Asiakas), "SukuNimi", b.SukuNimi)),
            profession: null,
            parentCompanyId: null, parentCompanyName: null);
    }

    private static CustomerDto BuildPersonDto(int asiakasNumero, string type, AsiakasBaseRow b,
                                               Dictionary<int, Counts> counts,
                                               CustomerFieldAccessSnapshot access,
                                               Type clrType,
                                               string? firstName, string? lastName,
                                               string? profession,
                                               string? parentCompanyId, string? parentCompanyName)
    {
        var snapshot = counts.GetValueOrDefault(asiakasNumero);
        var displayName = BuildPersonDisplayName(lastName, firstName, b.SukuNimi);
        return new CustomerDto(
            Id: asiakasNumero.ToString(CultureInfo.InvariantCulture),
            Type: type,
            DisplayName: displayName,
            Initials: Letters(lastName, firstName),
            Counts: new CustomerCountsDto(
                snapshot.Applications, snapshot.Reservations,
                snapshot.Contracts, snapshot.Offers, snapshot.Showings),
            PrimaryAddress: NullIfBlank(Allow(access, typeof(Asiakas), "KatuOsoite", b.KatuOsoite)),
            City:           NullIfBlank(Allow(access, typeof(Asiakas), "PostiToimiPaikka", b.PostiToimiPaikka)),
            Tag:            null,
            Phone:          NullIfBlank(Allow(access, typeof(Asiakas), "Gsm",     b.Gsm))
                            ?? NullIfBlank(Allow(access, typeof(Asiakas), "Puhelin", b.Puhelin)),
            Email:          NullIfBlank(Allow(access, typeof(Asiakas), "Email", b.Email)),
            FirstName: firstName,
            LastName:  lastName,
            CompanyName: null,
            BusinessId:  null,
            ParentCompanyId:   parentCompanyId,
            ParentCompanyName: parentCompanyName,
            PostalCode:      NullIfBlank(Allow(access, typeof(Asiakas), "PostiNumero", b.PostiNumero)),
            Country:         NullIfBlank(Allow(access, typeof(Asiakas), "Maa",         b.Maa)),
            Language:        NullIfBlank(Allow(access, typeof(Asiakas), "LangCode",    b.LangCode)),
            Profession:      profession,
            Industry:        NullIfBlank(Allow(access, typeof(Asiakas), "ToimiAla", b.ToimiAla)),
            Workplace:       NullIfBlank(Allow(access, typeof(Asiakas), "TyoPaikka", b.TyoPaikka)),
            Income:          Allow(access, typeof(Asiakas), "BruttoTulot",                b.BruttoTulot),
            EmailMarketingAllowed:    Allow(access, typeof(Asiakas), "EmailKayttoSallittu",     b.EmailKayttoSallittu),
            PhoneMarketingAllowed:    Allow(access, typeof(Asiakas), "PuhNoKayttoSallittu",     b.PuhNoKayttoSallittu),
            DirectMarketingForbidden: Allow(access, typeof(Asiakas), "Suoramarkkinointikielto", b.Suoramarkkinointikielto));
    }

    /// <summary>
    /// Returns <paramref name="value"/> when the access snapshot allows
    /// reading <paramref name="property"/> on <paramref name="clrType"/>;
    /// the type's default (null for refs / Nullable) otherwise. The
    /// AllowAll fast-path skips the dictionary lookup entirely.
    /// </summary>
    private static T? Allow<T>(CustomerFieldAccessSnapshot access, Type clrType, string property, T? value) =>
        access.AllAllowed || access.CanRead(clrType, property) ? value : default;

    private static string BuildPersonDisplayName(string? lastName, string? firstName, string? fallback)
    {
        var ln = (lastName ?? string.Empty).Trim();
        var fn = (firstName ?? string.Empty).Trim();
        if (ln.Length > 0 && fn.Length > 0) return $"{ln}, {fn}";
        if (ln.Length > 0) return ln;
        if (fn.Length > 0) return fn;
        return NullIfBlank(fallback) ?? "(nimetön)";
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

    /// <summary>Coerces SelectData scalar cells to int? regardless of source type.</summary>
    private static int? ToInt(object? value) => value switch
    {
        null            => null,
        int i           => i,
        long l          => unchecked((int)l),
        short s         => s,
        byte b          => b,
        decimal d       => unchecked((int)d),
        IConvertible cv => cv.ToInt32(CultureInfo.InvariantCulture),
        _               => null,
    };

    /// <summary>
    /// Nullable variant of <see cref="ToBoolish"/> for genuine bool columns
    /// (EmailKayttoSallittu, PuhNoKayttoSallittu, …) — null stays null so
    /// the wire DTO can distinguish "unset" from "false".
    /// </summary>
    private static bool? ToBool(object? value) => value switch
    {
        null            => null,
        bool b          => b,
        int i           => i != 0,
        long l          => l != 0,
        short s         => s != 0,
        byte by         => by != 0,
        string str when string.IsNullOrEmpty(str) => null,
        string str      => str != "0" && !string.Equals(str, "false", StringComparison.OrdinalIgnoreCase),
        IConvertible cv => cv.ToInt32(CultureInfo.InvariantCulture) != 0,
        _               => null,
    };

    /// <summary>
    /// XPO returns IIF-derived alias columns (OnHenkilo, OnYritys) as
    /// "0"/"1" strings, ints, or actual booleans depending on driver.
    /// Treat truthy values uniformly.
    /// </summary>
    private static bool ToBoolish(object? value) => value switch
    {
        null            => false,
        bool b          => b,
        int i           => i != 0,
        long l          => l != 0,
        short s         => s != 0,
        byte by         => by != 0,
        string str      => str.Length > 0 && str != "0" && !string.Equals(str, "false", StringComparison.OrdinalIgnoreCase),
        IConvertible cv => cv.ToInt32(CultureInfo.InvariantCulture) != 0,
        _               => false,
    };

    // -- helpers ----------------------------------------------------------

    private static CustomersResponseDto Empty() =>
        new(Array.Empty<CustomerDto>(), 0);

    private (xVasuSecuritySystemUser? user, IObjectSpace? os, CustomerFieldAccessSnapshot access)
        ResolveUser(ClaimsPrincipal principal)
    {
        var os = objectSpaceProvider.CreateObjectSpace();

        var email = EmailResolver.ResolveEmail(principal);
        if (string.IsNullOrEmpty(email))
        {
            logger.LogWarning("Customers request rejected — principal had no resolvable email");
            os.Dispose();
            return (null, null, CustomerFieldAccessSnapshot.DenyAll);
        }

        var variants = EmailResolver.BuildEmailVariants(email);
        var resolved = os.FindObject<xVasuSecuritySystemUser>(EmailResolver.BuildUserCriteria(variants, email));
        if (resolved is null)
        {
            logger.LogInformation("Customers request rejected — user {Email} not provisioned", email);
            os.Dispose();
            return (null, null, CustomerFieldAccessSnapshot.DenyAll);
        }

        // Snapshot is computed once per request from the open object
        // space — the role walk uses the same XPO Session so any roles
        // collection that has been lazily loaded reuses connection state.
        var access = fieldAccessPolicy.Evaluate(resolved);
        return (resolved, os, access);
    }
}
