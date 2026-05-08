using System.Security.Claims;
using DevExpress.Data.Filtering;
using DevExpress.ExpressApp;
using DevExpress.ExpressApp.Xpo;
using DevExpress.Xpo;
using DevExpress.Xpo.DB;
using mVasu.Api.Authentication;
using mVasu.Api.Common;
using mVasu.Api.Contracts;
using xVasu.Data.Security;
using XpoShowing = xVasu.Data.Kire.HuoneistoEsittelyTiedot;
using XpoTiskilista = xVasu.Data.Asutus.Tiskilista;

namespace mVasu.Api.Tiskilista;

/// <summary>
/// Reads <c>xVasu.Data.Asutus.Tiskilista</c> rows for the authenticated user.
/// "omat" scope filters by the user's AluetoimistoOikeudet links; "kaikki"
/// returns every row. distance sort is computed in memory because XPO cannot
/// translate Haversine to SQL without modifying the xVasu type.
/// </summary>
public sealed class TiskilistaQueryService(
    IObjectSpaceProvider objectSpaceProvider,
    ILogger<TiskilistaQueryService> logger) : ITiskilistaQueryService
{
    /// <summary>
    /// Hard ceiling on the FTS pre-pass result list. Same rationale as
    /// the customers FTS path: 1000 ids is well past the point a human
    /// can scan and far below SQL's IN-list limit.
    /// </summary>
    private const int FtsResultCap = 1000;

    private static readonly TimeZoneInfo HelsinkiTz = ResolveHelsinkiTimeZone();

    private static TimeZoneInfo ResolveHelsinkiTimeZone()
    {
        try { return TimeZoneInfo.FindSystemTimeZoneById("Europe/Helsinki"); }
        catch { return TimeZoneInfo.FindSystemTimeZoneById("FLE Standard Time"); }
    }

    public Task<TiskilistaPageDto?> ListAsync(
        ClaimsPrincipal principal,
        TiskilistaListQuery query,
        CancellationToken cancellationToken = default)
    {
        var (user, os) = ResolveUser(principal);
        if (user is null || os is null)
        {
            return Task.FromResult<TiskilistaPageDto?>(null);
        }

        try
        {
            var session = ((XPObjectSpace)os).Session;

            // Compose: filter criteria (always present) AND search
            // criteria (FTS-driven, only when query.Search is non-empty).
            // Splitting these lets BuildCriteria stay XPO-criteria-pure
            // while BuildSearchCriteria does the raw SQL FTS pre-pass.
            var baseCriteria = BuildCriteria(user, query);
            var searchCriteria = BuildSearchCriteria(session, query.Search);
            var criteria = searchCriteria is null
                ? baseCriteria
                : CriteriaOperator.And(baseCriteria, searchCriteria);

            // Distance sort: load the full filtered set and rank in memory.
            // For non-distance sorts use SQL-level ordering + skip/top.
            if (string.Equals(query.SortBy, "distance", StringComparison.OrdinalIgnoreCase)
                && query.UserLat.HasValue && query.UserLon.HasValue)
            {
                var allMatching = new XPCollection<XpoTiskilista>(session, criteria);
                var ranked = allMatching
                    .Select(t => (Row: t, Distance: ComputeDistanceKm(query.UserLat.Value, query.UserLon.Value, t.Latitude, t.Longitude)))
                    .OrderBy(r => r.Distance ?? double.MaxValue)
                    .ToList();

                var total = ranked.Count;
                var skipped = (query.Page - 1) * query.PageSize;
                var pageRows = ranked.Skip(skipped).Take(query.PageSize).ToList();
                var distanceUpcoming = LookupUpcomingShowings(session, CollectHuoneistoOids(pageRows.Select(r => r.Row)));
                var paged = pageRows
                    .Select(r => MapCard(r.Row, r.Distance, distanceUpcoming))
                    .ToList();

                return Task.FromResult<TiskilistaPageDto?>(
                    new TiskilistaPageDto(paged, total, query.Page, query.PageSize));
            }

            var totalCount = os.GetObjectsCount(typeof(XpoTiskilista), criteria);
            var skip = (query.Page - 1) * query.PageSize;

            var collection = new XPCollection<XpoTiskilista>(session, criteria)
            {
                SkipReturnedObjects = skip,
                TopReturnedObjects = query.PageSize,
            };
            foreach (var sp in BuildSorting(query.SortBy))
            {
                collection.Sorting.Add(sp);
            }

            var pageList = collection.ToList();
            var upcoming = LookupUpcomingShowings(session, CollectHuoneistoOids(pageList));
            var items = pageList
                .Select(t => MapCard(t, ComputeDistanceFor(t, query), upcoming))
                .ToList();

            return Task.FromResult<TiskilistaPageDto?>(
                new TiskilistaPageDto(items, totalCount, query.Page, query.PageSize));
        }
        finally
        {
            os.Dispose();
        }
    }

    public Task<TiskilistaDistinctValuesDto?> GetDistinctValuesAsync(
        ClaimsPrincipal principal,
        CancellationToken cancellationToken = default)
    {
        var (user, os) = ResolveUser(principal);
        if (user is null || os is null)
        {
            return Task.FromResult<TiskilistaDistinctValuesDto?>(null);
        }

        try
        {
            var scope = UserAreaScope.Resolve(user);
            CriteriaOperator? criteria = scope.Unrestricted
                ? null
                : scope.Areas.Count == 0
                    ? new BinaryOperator("OID", Guid.Empty, BinaryOperatorType.Equal)
                    : new InOperator("BranchCode", scope.Areas.Cast<object>().ToArray());

            var session = ((XPObjectSpace)os).Session;
            var collection = criteria is null
                ? new XPCollection<XpoTiskilista>(session)
                : new XPCollection<XpoTiskilista>(session, criteria);

            // Materialise once, project to distinct lists in memory.
            // 6k rows is fine; 5 separate DISTINCT round-trips would be
            // chattier than one collection load + LINQ Distinct.
            var rows = collection.ToArray();

            return Task.FromResult<TiskilistaDistinctValuesDto?>(new TiskilistaDistinctValuesDto(
                Lajit: DistinctSorted(rows, t => t.laji),
                Tyypit: DistinctSorted(rows, t => t.tyyppi),
                Kunnat: DistinctSorted(rows, t => t.kunta),
                Kaupunginosat: DistinctSorted(rows, t => t.KuntaAlue),
                KaupunginosatByKunta: DistinctKuntaKaupunginosaPairs(rows),
                Sopimustilat: DistinctSorted(rows, t => t.SopimusTila),
                Isannoitsijat: DistinctSorted(rows, t => t.Isannoitsija),
                Markkinoijat: DistinctSorted(rows, t => t.Markkinoija),
                Tilat: DistinctSorted(rows, t => t.Tila)));
        }
        finally
        {
            os.Dispose();
        }
    }

    private static IReadOnlyList<TiskilistaKuntaKaupunginosaDto> DistinctKuntaKaupunginosaPairs(
        XpoTiskilista[] rows)
    {
        var fi = StringComparer.Create(System.Globalization.CultureInfo.GetCultureInfo("fi-FI"), ignoreCase: true);
        return rows
            .Where(r => !string.IsNullOrWhiteSpace(r.kunta) && !string.IsNullOrWhiteSpace(r.KuntaAlue))
            .Select(r => new TiskilistaKuntaKaupunginosaDto(r.kunta!.Trim(), r.KuntaAlue!.Trim()))
            .DistinctBy(p => $"{p.Kunta}|{p.Kaupunginosa}", StringComparer.OrdinalIgnoreCase)
            .OrderBy(p => p.Kunta, fi)
            .ThenBy(p => p.Kaupunginosa, fi)
            .ToArray();
    }

    private static IReadOnlyList<string> DistinctSorted(
        XpoTiskilista[] rows, Func<XpoTiskilista, string?> selector) =>
        rows.Select(selector)
            .Where(v => !string.IsNullOrWhiteSpace(v))
            .Select(v => v!.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(v => v, StringComparer.Create(System.Globalization.CultureInfo.GetCultureInfo("fi-FI"), ignoreCase: true))
            .ToArray();

    public Task<TiskilistaDetailDto?> GetAsync(
        ClaimsPrincipal principal,
        Guid id,
        double? userLat = null,
        double? userLon = null,
        CancellationToken cancellationToken = default)
    {
        var (user, os) = ResolveUser(principal);
        if (user is null || os is null)
        {
            return Task.FromResult<TiskilistaDetailDto?>(null);
        }

        try
        {
            XpoTiskilista? row;
            try
            {
                row = os.GetObjectByKey<XpoTiskilista>(id);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Tiskilista {Id} failed to load — XPO threw on GetObjectByKey", id);
                return Task.FromResult<TiskilistaDetailDto?>(null);
            }

            if (row is null) return Task.FromResult<TiskilistaDetailDto?>(null);

            var session = ((XPObjectSpace)os).Session;
            var upcoming = LookupUpcomingShowings(session, CollectHuoneistoOids(new[] { row }));
            var distanceKm = userLat.HasValue && userLon.HasValue
                ? ComputeDistanceKm(userLat.Value, userLon.Value, row.Latitude, row.Longitude)
                : null;

            try
            {
                return Task.FromResult<TiskilistaDetailDto?>(MapDetail(row, upcoming, distanceKm));
            }
            catch (Exception ex)
            {
                // Live data has dangling FK targets that crash eager XPO loads
                // (CannotLoadObjectsException, etc.). Fall back to a minimal
                // mapping that only reads fields directly on Tiskilista — the
                // user gets *something* rather than a hard 500.
                logger.LogWarning(ex, "Tiskilista {Id} full-detail mapping failed; falling back to minimal", id);
                try
                {
                    return Task.FromResult<TiskilistaDetailDto?>(MapDetailMinimal(row, upcoming, distanceKm));
                }
                catch (Exception minimalEx)
                {
                    logger.LogError(minimalEx, "Tiskilista {Id} minimal mapping also failed", id);
                    return Task.FromResult<TiskilistaDetailDto?>(null);
                }
            }
        }
        finally
        {
            os.Dispose();
        }
    }

    private (xVasuSecuritySystemUser? user, IObjectSpace? os) ResolveUser(ClaimsPrincipal principal)
    {
        var os = objectSpaceProvider.CreateObjectSpace();

        var email = EmailResolver.ResolveEmail(principal);
        if (string.IsNullOrEmpty(email))
        {
            logger.LogWarning("Tiskilista request rejected — principal had no resolvable email");
            os.Dispose();
            return (null, null);
        }

        var variants = EmailResolver.BuildEmailVariants(email);
        var resolved = os.FindObject<xVasuSecuritySystemUser>(EmailResolver.BuildUserCriteria(variants, email));
        if (resolved is null)
        {
            logger.LogInformation("Tiskilista request rejected — user {Email} not provisioned", email);
            os.Dispose();
            return (null, null);
        }

        return (resolved, os);
    }

    /// <summary>
    /// Builds the non-search portion of the Tiskilista filter (scope,
    /// status, dimensional dropdowns, range bounds, boolean toggles).
    /// Search predicate is built separately by
    /// <see cref="BuildSearchCriteria"/> because it goes through the
    /// SQL Server FULLTEXT pass — see <see cref="LoadFtsTiskilistaOids"/>.
    /// </summary>
    private static CriteriaOperator BuildCriteria(xVasuSecuritySystemUser user, TiskilistaListQuery query)
    {
        var operands = new List<CriteriaOperator>();

        if (string.Equals(query.Scope, "omat", StringComparison.OrdinalIgnoreCase))
        {
            // Tiskilista.BranchCode is the canonical area key compared against
            // the user's Kayttooikeudet branchcodes. UserAreaScope collapses
            // the user's permission codes — code "999" means "all areas, no
            // restriction" so we skip the area filter entirely.
            var scope = UserAreaScope.Resolve(user);
            if (!scope.Unrestricted)
            {
                if (scope.Areas.Count == 0)
                {
                    // User has no branchcodes — return guaranteed-empty result.
                    operands.Add(new BinaryOperator("OID", Guid.Empty, BinaryOperatorType.Equal));
                }
                else
                {
                    operands.Add(new InOperator("BranchCode", scope.Areas.Cast<object>().ToArray()));
                }
            }
        }

        if (!string.IsNullOrWhiteSpace(query.Status))
        {
            operands.Add(new BinaryOperator("Tila", query.Status, BinaryOperatorType.Equal));
        }

        AddInFilter(operands, "laji", query.Lajit);
        AddInFilter(operands, "tyyppi", query.Tyypit);
        AddInFilter(operands, "kunta", query.Kunnat);
        AddInFilter(operands, "KuntaAlue", query.Kaupunginosat);
        AddInFilter(operands, "SopimusTila", query.Sopimustilat);
        AddInFilter(operands, "Isannoitsija", query.Isannoitsijat);
        AddInFilter(operands, "Markkinoija", query.Markkinoijat);

        if (query.OnKuvausTarveOnly == true)
        {
            // Bool filter — only surface rows that need photography. The flag
            // lives on the linked Huoneisto, not on Tiskilista itself.
            operands.Add(new BinaryOperator("Huoneisto.OnKuvausTarve", true, BinaryOperatorType.Equal));
        }
        if (query.HasUpcomingEsittelyOnly == true)
        {
            // Surface only rows whose Huoneisto has at least one upcoming
            // (non-cancelled, non-handled) showing. Walks Huoneisto.Esittelyt.
            var nowLocal = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, HelsinkiTz);
            operands.Add(new ContainsOperator("Huoneisto.Esittelyt",
                CriteriaOperator.And(
                    new BinaryOperator("EsittelyCancelled", false),
                    new BinaryOperator("EsittelyHandled", false),
                    new BinaryOperator("SystemCancelled", false),
                    new BinaryOperator("EsittelyAika", nowLocal, BinaryOperatorType.GreaterOrEqual))));
        }
        if (query.LumoFiOnly == true)
        {
            // Model.xafml uses Huoneisto.LumoOneEnabled for the "Lumo.fi" flag
            // — that's the canonical "published on Lumo.fi" boolean. The local
            // Tiskilista.InternetMarkkinointi is a broader "internet marketing
            // consent" flag that doesn't always coincide with Lumo.fi listing.
            operands.Add(new BinaryOperator("Huoneisto.LumoOneEnabled", true, BinaryOperatorType.Equal));
        }

        if (query.NeliotMin is { } neliotMin)
        {
            operands.Add(new BinaryOperator("neliot", neliotMin, BinaryOperatorType.GreaterOrEqual));
        }
        if (query.NeliotMax is { } neliotMax)
        {
            operands.Add(new BinaryOperator("neliot", neliotMax, BinaryOperatorType.LessOrEqual));
        }

        if (query.VapautuuFrom is { } vfFrom)
        {
            operands.Add(new BinaryOperator(
                "vapautuu", vfFrom.ToDateTime(TimeOnly.MinValue), BinaryOperatorType.GreaterOrEqual));
        }
        if (query.VapautuuTo is { } vfTo)
        {
            operands.Add(new BinaryOperator(
                "vapautuu", vfTo.ToDateTime(new TimeOnly(23, 59, 59)), BinaryOperatorType.LessOrEqual));
        }

        // Search predicate intentionally NOT included here — see
        // BuildSearchCriteria, which combines a SQL Server FULLTEXT
        // pre-pass with the int.TryParse fallback for kptunnus /
        // huonetunnus jumps.

        return operands.Count == 0
            ? new BinaryOperator("OID", Guid.Empty, BinaryOperatorType.NotEqual)
            : CriteriaOperator.And(operands);
    }

    /// <summary>
    /// Builds the search portion of the Tiskilista criteria. Strategy:
    /// run a FULLTEXT pre-pass against <c>dbo.t_tiskilista</c> (covers
    /// 12 indexed columns including <c>katuosoite</c>, <c>kunta</c>,
    /// <c>laji</c>, <c>tyyppi</c>, <c>tila</c>, <c>Markkinointialue</c>,
    /// <c>Markkinoija</c>, <c>Isannoitsija</c>, <c>LisaTieto</c>,
    /// <c>muistio</c>); OR-in with the kptunnus / huonetunnus digit
    /// fallback so power users can still jump to a numbered apartment.
    /// </summary>
    /// <returns>
    /// <para><c>null</c> when there's no search term — the caller skips
    /// the AND-combine step entirely.</para>
    /// <para>A criterion guaranteed to match nothing
    /// (<c>OID = Guid.Empty</c>) when the FTS pass yielded no hits and
    /// the input isn't a number — the caller still applies it so the
    /// totalCount comes back zero without further DB chatter.</para>
    /// </returns>
    private CriteriaOperator? BuildSearchCriteria(Session session, string? rawSearch)
    {
        if (string.IsNullOrWhiteSpace(rawSearch)) return null;
        var search = rawSearch.Trim();

        var operands = new List<CriteriaOperator>();

        var ftsOids = LoadFtsTiskilistaOids(session, search);
        if (ftsOids.Count > 0)
        {
            operands.Add(new InOperator("OID", ftsOids.Cast<object>().ToArray()));
        }

        // Digit fallback — pure-int input maps to exact kptunnus or
        // huonetunnus match independently of the FTS pass. Power users
        // type "12345" expecting a direct hit on the numbered building.
        if (int.TryParse(search, out var num))
        {
            operands.Add(new BinaryOperator("kptunnus",    num, BinaryOperatorType.Equal));
            operands.Add(new BinaryOperator("huonetunnus", num, BinaryOperatorType.Equal));
        }

        if (operands.Count == 0)
        {
            // Nothing matched → return a guaranteed-empty predicate
            // rather than null so the count + page paths produce zero
            // hits without round-tripping further.
            return new BinaryOperator("OID", Guid.Empty, BinaryOperatorType.Equal);
        }

        return operands.Count == 1 ? operands[0] : CriteriaOperator.Or(operands.ToArray());
    }

    /// <summary>
    /// Runs SQL Server <c>CONTAINS</c> against the existing Tiskilista
    /// fulltext catalog (same <c>fts_Asiakas</c> catalog as Asiakas;
    /// the catalog is shared, the index per table is separate). The
    /// catalog covers the 12 Tiskilista columns documented in the
    /// vasu-sql-schema skill, indexed in Finnish (LCID 1053) so
    /// stemming and inflection are handled by the engine.
    /// </summary>
    private List<Guid> LoadFtsTiskilistaOids(Session session, string searchTerm)
    {
        var ftsExpression = FtsExpressionBuilder.Build(searchTerm);
        if (ftsExpression is null) return new List<Guid>();

        var classInfo = session.GetClassInfo(typeof(XpoTiskilista));
        var qualifiedTable = XpoTableNameResolver.Qualified(classInfo.TableName);

        var sql =
            $"SELECT TOP {FtsResultCap} [OID] " +
            $"FROM {qualifiedTable} " +
            "WHERE CONTAINS((" +
            "[katuosoite], [kunta], [KuntaAlue], [Aluetoimisto], " +
            "[laji], [tyyppi], [tila], " +
            "[Markkinointialue], [Markkinoija], [Isannoitsija], " +
            "[LisaTieto], [muistio]" +
            "), @ftsTerm)";

        SelectedData data;
        try
        {
            data = session.ExecuteQuery(sql,
                new[] { "@ftsTerm" },
                new object[] { ftsExpression });
        }
        catch (Exception ex)
        {
            // FTS catalog disabled or missing? Bail safely — empty list
            // yields a zero-result UI rather than 500.
            logger.LogWarning(ex,
                "Tiskilista FTS query failed for term {Term} — returning empty match list",
                searchTerm);
            return new List<Guid>();
        }

        var oids = new List<Guid>();
        if (data.ResultSet.Length == 0) return oids;

        foreach (var row in data.ResultSet[0].Rows)
        {
            if (row.Values.Length == 0 || row.Values[0] is null) continue;
            switch (row.Values[0])
            {
                case Guid g: oids.Add(g); break;
                case string s when Guid.TryParse(s, out var parsed): oids.Add(parsed); break;
            }
        }
        return oids;
    }

    private static void AddInFilter(List<CriteriaOperator> operands, string property, IReadOnlyList<string>? values)
    {
        if (values is not { Count: > 0 }) return;
        var arr = values.Where(s => !string.IsNullOrWhiteSpace(s)).Cast<object>().ToArray();
        if (arr.Length > 0) operands.Add(new InOperator(property, arr));
    }

    private static SortProperty[] BuildSorting(string sortBy) => sortBy.ToLowerInvariant() switch
    {
        "osoite" => [new SortProperty("katuosoite", DevExpress.Xpo.DB.SortingDirection.Ascending)],
        "vuokra" => [new SortProperty("vuokra", DevExpress.Xpo.DB.SortingDirection.Ascending)],
        _ => [new SortProperty("vapautuu", DevExpress.Xpo.DB.SortingDirection.Ascending)],
    };

    private static IEnumerable<Guid> CollectHuoneistoOids(IEnumerable<XpoTiskilista> rows)
    {
        foreach (var t in rows)
        {
            Guid? oid = null;
            try { oid = t.Huoneisto?.OID; }
            catch { /* dangling FK on Huoneisto — skip */ }
            if (oid.HasValue) yield return oid.Value;
        }
    }

    /// <summary>
    /// Pre-loads the next upcoming (non-cancelled, non-handled) showing for a
    /// batch of Huoneisto OIDs in a single XPO query. Used so MapCard can
    /// surface "Esittely tulossa" without firing one query per row.
    /// </summary>
    private static IDictionary<Guid, DateTime> LookupUpcomingShowings(
        Session session, IEnumerable<Guid> huoneistoOids)
    {
        var oidArr = huoneistoOids.Distinct().Cast<object>().ToArray();
        var result = new Dictionary<Guid, DateTime>();
        if (oidArr.Length == 0) return result;

        var nowLocal = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, HelsinkiTz);
        var criteria = CriteriaOperator.And(
            new InOperator("Huoneisto.OID", oidArr),
            new BinaryOperator("EsittelyCancelled", false),
            new BinaryOperator("EsittelyHandled", false),
            new BinaryOperator("SystemCancelled", false),
            new BinaryOperator("EsittelyAika", nowLocal, BinaryOperatorType.GreaterOrEqual));

        try
        {
            var collection = new XPCollection<XpoShowing>(session, criteria);
            foreach (var s in collection)
            {
                var hOid = s.Huoneisto?.OID;
                if (hOid is null) continue;
                if (!result.TryGetValue(hOid.Value, out var existing) || s.EsittelyAika < existing)
                {
                    result[hOid.Value] = s.EsittelyAika;
                }
            }
        }
        catch (Exception)
        {
            // Live data referential issues — return what we have so far.
        }
        return result;
    }

    private static double? ComputeDistanceFor(XpoTiskilista row, TiskilistaListQuery query) =>
        query.UserLat.HasValue && query.UserLon.HasValue
            ? ComputeDistanceKm(query.UserLat.Value, query.UserLon.Value, row.Latitude, row.Longitude)
            : null;

    private static double? ComputeDistanceKm(double userLat, double userLon, double rowLat, double rowLon)
    {
        if (rowLat == 0 && rowLon == 0) return null;

        const double earthRadiusKm = 6371.0;
        var dLat = ToRadians(rowLat - userLat);
        var dLon = ToRadians(rowLon - userLon);
        var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2)
              + Math.Cos(ToRadians(userLat)) * Math.Cos(ToRadians(rowLat))
              * Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
        var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
        return earthRadiusKm * c;
    }

    private static double ToRadians(double degrees) => degrees * Math.PI / 180.0;

    private static DateTimeOffset? ToOffset(DateTime value) =>
        value == default ? null : new DateTimeOffset(DateTime.SpecifyKind(value, DateTimeKind.Local));

    private static TiskilistaCardDto MapCard(
        XpoTiskilista t, double? distanceKm, IDictionary<Guid, DateTime> upcoming) => new(
        Id: t.OID,
        Osoite: t.katuosoite ?? string.Empty,
        Kptunnus: t.kptunnus == 0 ? null : t.kptunnus,
        Huonetunnus: t.huonetunnus == 0 ? null : t.huonetunnus,
        Tyyppi: t.tyyppi,
        Laji: t.laji,
        Vuokra: t.vuokra,
        Vapautuu: ToOffset(t.vapautuu),
        Neliot: t.neliot,
        Kerros: t.kerros,
        Kerroksia: t.kerroksia,
        Tila: t.Tila,
        SopimusTila: t.SopimusTila,
        Kunta: t.kunta,
        Kaupunginosa: t.KuntaAlue,
        Prio: NormaliseString(t.Huoneisto?.SAP_Palveluluokka),
        Isannoitsija: NormaliseString(t.Isannoitsija),
        Markkinoija: NormaliseString(t.Markkinoija),
        // Lumo.fi is the canonical Huoneisto.LumoOneEnabled flag (XAF
        // ListView column 0). Tiskilista.InternetMarkkinointi is a broader
        // "marketing consent" flag and does not match the Lumo.fi semantics.
        LumoFi: t.Huoneisto?.LumoOneEnabled ?? false,
        Vuokraovi: t.Vuokraovi,
        OnKuvausTarve: t.Huoneisto?.OnKuvausTarve ?? false,
        Hissi: t.hissi,
        Parveke: t.parveke,
        Sauna: t.sauna,
        NextEsittelyAt: NextEsittelyFor(t, upcoming),
        Latitude: t.Latitude == 0 ? null : t.Latitude,
        Longitude: t.Longitude == 0 ? null : t.Longitude,
        DistanceKm: distanceKm);

    private static DateTimeOffset? NextEsittelyFor(
        XpoTiskilista t, IDictionary<Guid, DateTime> upcoming)
    {
        try
        {
            var hOid = t.Huoneisto?.OID;
            return hOid is null
                ? null
                : (upcoming.TryGetValue(hOid.Value, out var next) ? ToOffset(next) : null);
        }
        catch
        {
            return null;
        }
    }

    private static string? NormaliseString(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value;

    private static TiskilistaDetailDto MapDetail(
        XpoTiskilista t, IDictionary<Guid, DateTime> upcoming, double? distanceKm) => new(
        Id: t.OID,
        Osoite: t.katuosoite ?? string.Empty,
        Kptunnus: t.kptunnus == 0 ? null : t.kptunnus,
        Huonetunnus: t.huonetunnus == 0 ? null : t.huonetunnus,
        Postinumero: t.Postinumero,
        Postitoimipaikka: t.Postitoimipaikka,
        Tyyppi: t.tyyppi,
        Laji: t.laji,
        Vuokra: t.vuokra,
        Vapautuu: ToOffset(t.vapautuu),
        Poismuutto: ToOffset(t.poismuutto),
        VapautuuAsiakkaalta: ToOffset(t.VapautuuAsiakkaalta),
        RemonttiAlkaa: ToOffset(t.RemontinAlkamispaiva),
        RemonttiPaattyy: ToOffset(t.RemontinPaattymispaiva),
        Remonttityyppi: NormaliseString(t.Remonttityyppi),
        Neliot: t.neliot,
        Kerros: t.kerros,
        Kerroksia: t.kerroksia,
        Tila: t.Tila,
        SopimusTila: t.SopimusTila,
        Kunta: t.kunta,
        Kaupunginosa: t.KuntaAlue,
        Markkinointialue: t.Markkinointialue,
        Prio: NormaliseString(t.Huoneisto?.SAP_Palveluluokka),
        Isannoitsija: NormaliseString(t.Isannoitsija),
        Markkinoija: NormaliseString(t.Markkinoija),
        TarkastusTila: ResolveTarkastusTila(t),
        // Match XAF Model.xafml — Lumo.fi flag is Huoneisto.LumoOneEnabled.
        LumoFi: t.Huoneisto?.LumoOneEnabled ?? false,
        Vuokraovi: t.Vuokraovi,
        OnKuvausTarve: t.Huoneisto?.OnKuvausTarve ?? false,
        Muistio: t.muistio,
        HuoneistoMuistio: NormaliseString(t.Huoneisto?.Muistio),
        Kuvaus: t.kuvaus,
        LisaTieto: t.LisaTieto,
        BrochureUrl: NormaliseString(t.Huoneisto?.BrochureUrl),
        Hissi: t.hissi,
        Parveke: t.parveke,
        Sauna: t.sauna,
        YhteissaUna: t.yhtsauna,
        Vesimittaus: t.vesimittaus,
        Pesula: t.pesula,
        Astianpesukone: t.astianpesukone,
        Aluetoimisto: t.Aluetoimisto,
        NextEsittelyAt: NextEsittelyFor(t, upcoming),
        Latitude: t.Latitude == 0 ? null : t.Latitude,
        Longitude: t.Longitude == 0 ? null : t.Longitude,
        DistanceKm: distanceKm,
        LumoUrl: t.Huoneisto?.LumoUrl);

    private static string? ResolveTarkastusTila(XpoTiskilista t)
    {
        // Huoneisto.LastHuoneistoTarkastusPeriodic is a non-persistent
        // computed property; calling it can hit the DB. Wrap defensively
        // since Tarkastukset history can be missing or malformed.
        try
        {
            return NormaliseString(t.Huoneisto?.LastHuoneistoTarkastusPeriodic?.TarkastuksenTila);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Fallback projection that reads only fields directly on Tiskilista.
    /// Used when MapDetail throws — typically when an associated row
    /// (Huoneisto, Tarkastukset, Talousyksikkö) has dangling FK targets
    /// that crash XPO's eager-load. The user still sees the apartment
    /// rather than a hard 500.
    /// </summary>
    private static TiskilistaDetailDto MapDetailMinimal(
        XpoTiskilista t, IDictionary<Guid, DateTime> upcoming, double? distanceKm) => new(
        Id: t.OID,
        Osoite: t.katuosoite ?? string.Empty,
        Kptunnus: t.kptunnus == 0 ? null : t.kptunnus,
        Huonetunnus: t.huonetunnus == 0 ? null : t.huonetunnus,
        Postinumero: t.Postinumero,
        Postitoimipaikka: t.Postitoimipaikka,
        Tyyppi: t.tyyppi,
        Laji: t.laji,
        Vuokra: t.vuokra,
        Vapautuu: ToOffset(t.vapautuu),
        Poismuutto: ToOffset(t.poismuutto),
        VapautuuAsiakkaalta: ToOffset(t.VapautuuAsiakkaalta),
        RemonttiAlkaa: ToOffset(t.RemontinAlkamispaiva),
        RemonttiPaattyy: ToOffset(t.RemontinPaattymispaiva),
        Remonttityyppi: NormaliseString(t.Remonttityyppi),
        Neliot: t.neliot,
        Kerros: t.kerros,
        Kerroksia: t.kerroksia,
        Tila: t.Tila,
        SopimusTila: t.SopimusTila,
        Kunta: t.kunta,
        Kaupunginosa: t.KuntaAlue,
        Markkinointialue: t.Markkinointialue,
        Prio: null,                                  // Huoneisto-traversal
        Isannoitsija: NormaliseString(t.Isannoitsija),
        Markkinoija: NormaliseString(t.Markkinoija),
        TarkastusTila: null,                         // Huoneisto-traversal
        LumoFi: false,                               // Huoneisto-traversal (Huoneisto.LumoOneEnabled)
        Vuokraovi: t.Vuokraovi,
        OnKuvausTarve: false,                        // Huoneisto-traversal
        Muistio: t.muistio,
        HuoneistoMuistio: null,                      // Huoneisto-traversal
        Kuvaus: t.kuvaus,
        LisaTieto: t.LisaTieto,
        BrochureUrl: null,                           // Huoneisto-traversal
        Hissi: t.hissi,
        Parveke: t.parveke,
        Sauna: t.sauna,
        YhteissaUna: t.yhtsauna,
        Vesimittaus: t.vesimittaus,
        Pesula: t.pesula,
        Astianpesukone: t.astianpesukone,
        Aluetoimisto: t.Aluetoimisto,
        NextEsittelyAt: NextEsittelyFor(t, upcoming),
        Latitude: t.Latitude == 0 ? null : t.Latitude,
        Longitude: t.Longitude == 0 ? null : t.Longitude,
        DistanceKm: distanceKm,
        LumoUrl: null);                              // Huoneisto-traversal
}
