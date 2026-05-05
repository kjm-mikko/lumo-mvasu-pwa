using System.Security.Claims;
using DevExpress.Data.Filtering;
using DevExpress.ExpressApp;
using DevExpress.ExpressApp.Xpo;
using DevExpress.Xpo;
using mVasu.Api.Authentication;
using mVasu.Api.Contracts;
using xVasu.Data.Security;
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
            var criteria = BuildCriteria(user, query);
            var session = ((XPObjectSpace)os).Session;

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
                var paged = ranked.Skip(skipped).Take(query.PageSize)
                    .Select(r => MapCard(r.Row, r.Distance))
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

            var items = collection
                .Select(t => MapCard(t, ComputeDistanceFor(t, query)))
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
                Sopimustilat: DistinctSorted(rows, t => t.SopimusTila),
                Isannoitsijat: DistinctSorted(rows, t => t.Isannoitsija),
                Tilat: DistinctSorted(rows, t => t.Tila)));
        }
        finally
        {
            os.Dispose();
        }
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
        CancellationToken cancellationToken = default)
    {
        var (user, os) = ResolveUser(principal);
        if (user is null || os is null)
        {
            return Task.FromResult<TiskilistaDetailDto?>(null);
        }

        try
        {
            var row = os.GetObjectByKey<XpoTiskilista>(id);
            return Task.FromResult(row is null ? null : MapDetail(row));
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

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.Trim();
            var searchOperands = new List<CriteriaOperator>
            {
                new FunctionOperator(FunctionOperatorType.Contains, new OperandProperty("katuosoite"), search),
                new FunctionOperator(FunctionOperatorType.Contains, new OperandProperty("kunta"), search),
                new FunctionOperator(FunctionOperatorType.Contains, new OperandProperty("KuntaAlue"), search),
                new FunctionOperator(FunctionOperatorType.Contains, new OperandProperty("Postitoimipaikka"), search),
            };

            // Pure digits → also match kptunnus / huonetunnus exactly. Most
            // power-users type a kptunnus to jump straight to a building.
            if (int.TryParse(search, out var num))
            {
                searchOperands.Add(new BinaryOperator("kptunnus", num, BinaryOperatorType.Equal));
                searchOperands.Add(new BinaryOperator("huonetunnus", num, BinaryOperatorType.Equal));
            }

            operands.Add(CriteriaOperator.Or(searchOperands.ToArray()));
        }

        return operands.Count == 0
            ? new BinaryOperator("OID", Guid.Empty, BinaryOperatorType.NotEqual)
            : CriteriaOperator.And(operands);
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

    private static TiskilistaCardDto MapCard(XpoTiskilista t, double? distanceKm) => new(
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
        LumoFi: t.InternetMarkkinointi,
        Vuokraovi: t.Vuokraovi,
        OnKuvausTarve: t.Huoneisto?.OnKuvausTarve ?? false,
        Hissi: t.hissi,
        Parveke: t.parveke,
        Sauna: t.sauna,
        Latitude: t.Latitude == 0 ? null : t.Latitude,
        Longitude: t.Longitude == 0 ? null : t.Longitude,
        DistanceKm: distanceKm);

    private static string? NormaliseString(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value;

    private static TiskilistaDetailDto MapDetail(XpoTiskilista t) => new(
        Id: t.OID,
        Osoite: t.katuosoite ?? string.Empty,
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
        LumoFi: t.InternetMarkkinointi,
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
        Latitude: t.Latitude == 0 ? null : t.Latitude,
        Longitude: t.Longitude == 0 ? null : t.Longitude,
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
}
