using System.Globalization;
using System.Security.Claims;
using DevExpress.ExpressApp;
using DevExpress.ExpressApp.Xpo;
using DevExpress.Xpo;
using DevExpress.Xpo.DB;
using mVasu.Api.Common;
using mVasu.Api.Contracts;
using xVasu.Data.Asma;
using XpoApartment  = xVasu.Data.Kire.Huoneisto;
using XpoContract   = xVasu.Data.Vuha.Sopimus;
using XpoTiskilista = xVasu.Data.Asutus.Tiskilista;

namespace mVasu.Api.Search;

/// <summary>
/// Long-tail global search backed by SQL Server FULLTEXT catalogs:
/// <list type="bullet">
///   <item>Huoneistot → <c>t_Huoneisto</c> (Street, Name, Code) —
///   filtered to residential (<c>HuoneistoLajiTunnus = 9001</c>)</item>
///   <item>Vapaat huoneistot → <c>t_tiskilista</c> (12 columns:
///   katuosoite, kunta, KuntaAlue, Aluetoimisto, laji, tyyppi, tila,
///   Markkinointialue, Markkinoija, Isannoitsija, LisaTieto,
///   muistio) — vacancy subset</item>
///   <item>Asukkaat → <c>t_Asiakas</c> (EtuNimi, SukuNimi, KatuOsoite,
///   Email, Gsm)</item>
///   <item>Sopimukset → <c>VSOPIMUS</c> (Tenants, Addresses, EMAIL,
///   VIITE — denormalised search fields populated by
///   <c>vvo_sp_update_sopimus_search_fields</c>)</item>
///   <item>Toiminnot → static catalogue of in-app commands</item>
/// </list>
/// One <see cref="IObjectSpace"/> is opened per request; the four FTS
/// helpers share its <see cref="Session"/> so a search hits the DB
/// four times rather than sixteen.
/// </summary>
public sealed class SearchService(
    IObjectSpaceProvider objectSpaceProvider,
    ILogger<SearchService> logger) : ISearchService
{
    private const int MinQueryLength = 2;

    /// <summary>
    /// Server cap on each FTS group's row count. Five hits is the
    /// default frontend limit; capping at 50 leaves headroom without
    /// unbounded payloads. All four FTS groups share this cap.
    /// </summary>
    private const int FtsGroupCap = 50;

    public Task<SearchResponseDto> SearchAsync(
        ClaimsPrincipal principal,
        SearchQueryParameters query,
        CancellationToken cancellationToken = default)
    {
        var needle = query.Q?.Trim() ?? string.Empty;
        if (needle.Length < MinQueryLength)
        {
            return Task.FromResult(new SearchResponseDto(Array.Empty<SearchGroupDto>()));
        }

        var ftsExpression = FtsExpressionBuilder.Build(needle);
        if (ftsExpression is null)
        {
            return Task.FromResult(new SearchResponseDto(Array.Empty<SearchGroupDto>()));
        }

        var limit = query.Limit > 0 ? query.Limit : 5;
        var groups = new List<SearchGroupDto>();

        IObjectSpace? os = null;
        try
        {
            os = objectSpaceProvider.CreateObjectSpace();
            var session = ((XPObjectSpace)os).Session;

            var apartmentsHits = LoadApartmentsHits(session, needle, ftsExpression, limit);
            if (apartmentsHits.Count > 0)
            {
                groups.Add(new SearchGroupDto(SearchGroupIds.Apartments, "Huoneistot", apartmentsHits));
            }

            var peopleHits = LoadPeopleHits(session, needle, ftsExpression, limit);
            if (peopleHits.Count > 0)
            {
                groups.Add(new SearchGroupDto(SearchGroupIds.People, "Asukkaat", peopleHits));
            }

            var unitsHits = LoadUnitsHits(session, needle, ftsExpression, limit);
            if (unitsHits.Count > 0)
            {
                // Label is "Vapaat huoneistot" because the source is
                // t_tiskilista — the denormalised hakutaulu populated
                // for available units only. A wider "Huoneistot" group
                // backed by t_Huoneisto FTS (with an asuinhuoneisto
                // filter) is on the roadmap; see memory note
                // project_search_huoneistot_expansion.md.
                groups.Add(new SearchGroupDto(SearchGroupIds.Units, "Vapaat huoneistot", unitsHits));
            }

            var contractsHits = LoadContractsHits(session, needle, ftsExpression, limit);
            if (contractsHits.Count > 0)
            {
                groups.Add(new SearchGroupDto(SearchGroupIds.Contracts, "Sopimukset", contractsHits));
            }
        }
        finally
        {
            os?.Dispose();
        }

        // Static action catalogue — no FTS, just substring match. Still
        // rendered last in canonical order so users land on data hits
        // first.
        var actionHits = ACTIONS
            .Where(a => MatchAction(a, needle))
            .Take(limit)
            .ToList();
        if (actionHits.Count > 0)
        {
            groups.Add(new SearchGroupDto(SearchGroupIds.Actions, "Toiminnot", actionHits));
        }

        // Sort by SCREENS.md §05 canonical order regardless of how
        // helpers contributed — frontend renders in this sequence.
        groups = groups
            .OrderBy(g => Array.IndexOf(CanonicalOrder, g.Id))
            .ToList();

        return Task.FromResult(new SearchResponseDto(groups));
    }

    // -- people (t_Asiakas FTS) --------------------------------------------

    private IReadOnlyList<SearchHitDto> LoadPeopleHits(
        Session session, string needle, string ftsExpression, int limit)
    {
        try
        {
            var classInfo = session.GetClassInfo(typeof(Asiakas));
            var qualifiedTable = XpoTableNameResolver.Qualified(classInfo.TableName);
            var top = Math.Min(limit, FtsGroupCap);

            var sql =
                $"SELECT TOP {top} " +
                "[AsiakasNumero], [SukuNimi], [KatuOsoite], [PostiToimiPaikka] " +
                $"FROM {qualifiedTable} " +
                "WHERE [GCRecord] IS NULL " +
                "  AND CONTAINS(([EtuNimi], [SukuNimi], [KatuOsoite], [Email], [Gsm]), @ftsTerm)";

            var data = session.ExecuteQuery(sql,
                new[] { "@ftsTerm" }, new object[] { ftsExpression });

            if (data.ResultSet.Length == 0) return Array.Empty<SearchHitDto>();

            var hits = new List<SearchHitDto>(data.ResultSet[0].Rows.Length);
            foreach (var row in data.ResultSet[0].Rows)
            {
                if (row.Values.Length < 4 || row.Values[0] is null) continue;

                var id          = Convert.ToInt32(row.Values[0]);
                var sukuNimi    = row.Values[1] as string;
                var katuOsoite  = row.Values[2] as string;
                var paikka      = row.Values[3] as string;

                hits.Add(new SearchHitDto(
                    Id:       id.ToString(CultureInfo.InvariantCulture),
                    Title:    string.IsNullOrWhiteSpace(sukuNimi) ? "(nimetön)" : sukuNimi.Trim(),
                    Meta:     JoinMeta(katuOsoite, paikka),
                    Icon:     "user",
                    Navigate: $"/customers/{id}"));
            }

            logger.LogInformation(
                "Search People FTS '{Needle}' (term '{Term}', table '{Table}') returned {Count} hits",
                needle, ftsExpression, qualifiedTable, hits.Count);
            return hits;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex,
                "Search FTS for People failed for {Needle} — returning empty group",
                needle);
            return Array.Empty<SearchHitDto>();
        }
    }

    // -- apartments (t_Huoneisto FTS, residential only) -------------------

    /// <summary>
    /// Hits from the apartment master <c>t_Huoneisto</c>, restricted
    /// to residential lajis (<c>HuoneistoLajiTunnus = 9001</c>) so
    /// parking spaces, saunas, storage units and other non-living
    /// spaces don't crowd the user's quick-search overlay. The FTS
    /// catalog covers Street, Name and Code; Title shows Street, the
    /// meta line surfaces unit type + city for context.
    /// </summary>
    /// <remarks>
    /// <para>Honours the caller's <c>limit</c> just like the other
    /// FTS groups (capped at <see cref="FtsGroupCap"/>) so the
    /// overlay payload stays consistent across categories.</para>
    ///
    /// <para>No <c>GCRecord IS NULL</c> filter — the legacy schema
    /// doesn't soft-delete on this table, mirroring the SQL pattern
    /// the user verified manually.</para>
    /// </remarks>
    private IReadOnlyList<SearchHitDto> LoadApartmentsHits(
        Session session, string needle, string ftsExpression, int limit)
    {
        try
        {
            var classInfo = session.GetClassInfo(typeof(XpoApartment));
            var qualifiedTable = XpoTableNameResolver.Qualified(classInfo.TableName);
            var top = Math.Min(limit, FtsGroupCap);

            var sql =
                $"SELECT TOP {top} " +
                "[OID], [Street], [HUONETYYPPITUNNUS], [City] " +
                $"FROM {qualifiedTable} " +
                "WHERE CONTAINS(([Street], [Name], [Code]), @ftsTerm) " +
                "  AND [HuoneistoLajiTunnus] = 9001";

            var data = session.ExecuteQuery(sql,
                new[] { "@ftsTerm" }, new object[] { ftsExpression });

            if (data.ResultSet.Length == 0) return Array.Empty<SearchHitDto>();

            var hits = new List<SearchHitDto>(data.ResultSet[0].Rows.Length);
            foreach (var row in data.ResultSet[0].Rows)
            {
                if (row.Values.Length < 4 || row.Values[0] is null) continue;

                var oid = row.Values[0] switch
                {
                    Guid g                                     => g,
                    string s when Guid.TryParse(s, out var p) => p,
                    _                                          => Guid.Empty,
                };
                if (oid == Guid.Empty) continue;

                var street       = row.Values[1] as string;
                var huonetyyppi  = row.Values[2] as string;
                var city         = row.Values[3] as string;

                hits.Add(new SearchHitDto(
                    Id:       oid.ToString(),
                    Title:    string.IsNullOrWhiteSpace(street) ? "(osoitteeton)" : street.Trim(),
                    Meta:     JoinMeta(huonetyyppi, city),
                    Icon:     "home",
                    // No apartment-detail route yet; frontend shows a
                    // "Tulossa" toast on click. /tiskilista/{OID} is the
                    // Tiskilista entity OID, not the Huoneisto OID, so
                    // routing there would 404.
                    Navigate: null));
            }

            logger.LogInformation(
                "Search Apartments FTS '{Needle}' (term '{Term}', table '{Table}') returned {Count} hits",
                needle, ftsExpression, qualifiedTable, hits.Count);
            return hits;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex,
                "Search FTS for Apartments failed for {Needle} — returning empty group",
                needle);
            return Array.Empty<SearchHitDto>();
        }
    }

    // -- units (t_tiskilista FTS) -----------------------------------------

    /// <summary>
    /// Search hits drawn from the denormalised Tiskilista hakutaulu —
    /// the <c>vvo_sp_create_tiskilista_*</c> SP family keeps it in sync
    /// with t_Huoneisto and the related dimensions, so we don't need to
    /// re-JOIN VSOPIMUS / Huoneisto here. <c>katuosoite</c> drives the
    /// hit Title; <c>kunta · tyyppi · tila</c> compose the meta line.
    /// </summary>
    private IReadOnlyList<SearchHitDto> LoadUnitsHits(
        Session session, string needle, string ftsExpression, int limit)
    {
        try
        {
            var classInfo = session.GetClassInfo(typeof(XpoTiskilista));
            var qualifiedTable = XpoTableNameResolver.Qualified(classInfo.TableName);
            var top = Math.Min(limit, FtsGroupCap);

            var sql =
                $"SELECT TOP {top} " +
                "[OID], [katuosoite], [kunta], [tyyppi], [tila] " +
                $"FROM {qualifiedTable} " +
                "WHERE CONTAINS((" +
                "[katuosoite], [kunta], [KuntaAlue], [Aluetoimisto], " +
                "[laji], [tyyppi], [tila], " +
                "[Markkinointialue], [Markkinoija], [Isannoitsija], " +
                "[LisaTieto], [muistio]" +
                "), @ftsTerm)";

            var data = session.ExecuteQuery(sql,
                new[] { "@ftsTerm" }, new object[] { ftsExpression });

            if (data.ResultSet.Length == 0) return Array.Empty<SearchHitDto>();

            var hits = new List<SearchHitDto>(data.ResultSet[0].Rows.Length);
            foreach (var row in data.ResultSet[0].Rows)
            {
                if (row.Values.Length < 5 || row.Values[0] is null) continue;

                var oid = row.Values[0] switch
                {
                    Guid g                                       => g,
                    string s when Guid.TryParse(s, out var p)   => p,
                    _                                            => Guid.Empty,
                };
                if (oid == Guid.Empty) continue;

                var katuOsoite = row.Values[1] as string;
                var kunta      = row.Values[2] as string;
                var tyyppi     = row.Values[3] as string;
                var tila       = row.Values[4] as string;

                hits.Add(new SearchHitDto(
                    Id:       oid.ToString(),
                    Title:    string.IsNullOrWhiteSpace(katuOsoite)
                                  ? "(osoitteeton)"
                                  : katuOsoite.Trim(),
                    Meta:     JoinMeta(kunta, tyyppi, tila),
                    Icon:     "home",
                    Navigate: $"/tiskilista/{oid}"));
            }

            logger.LogInformation(
                "Search Units FTS '{Needle}' (term '{Term}', table '{Table}') returned {Count} hits",
                needle, ftsExpression, qualifiedTable, hits.Count);
            return hits;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex,
                "Search FTS for Units failed for {Needle} — returning empty group",
                needle);
            return Array.Empty<SearchHitDto>();
        }
    }

    // -- contracts (VSOPIMUS FTS) -----------------------------------------

    /// <summary>
    /// Hits from the contract fulltext catalog. <c>VSOPIMUS.Tenants</c>
    /// and <c>Addresses</c> are denormalised search columns kept in
    /// sync by <c>vvo_sp_update_sopimus_search_fields</c> — searching
    /// them is the whole point of the index, so we read them straight
    /// out into the hit meta line. No contract detail page yet, so
    /// <see cref="SearchHitDto.Navigate"/> stays null and the frontend
    /// shows its "Tulossa" toast on click.
    /// </summary>
    private IReadOnlyList<SearchHitDto> LoadContractsHits(
        Session session, string needle, string ftsExpression, int limit)
    {
        try
        {
            var classInfo = session.GetClassInfo(typeof(XpoContract));
            var qualifiedTable = XpoTableNameResolver.Qualified(classInfo.TableName);
            var top = Math.Min(limit, FtsGroupCap);

            // VSOPIMUS doesn't ship with a GCRecord soft-delete column
            // in the legacy schema — leave it out so the query parses.
            // If a future xVasu version adds one, surface it here.
            var sql =
                $"SELECT TOP {top} " +
                "[VSOPTUNNUS], [Tenants], [Addresses] " +
                $"FROM {qualifiedTable} " +
                "WHERE CONTAINS(([Tenants], [Addresses], [EMAIL], [VIITE]), @ftsTerm)";

            var data = session.ExecuteQuery(sql,
                new[] { "@ftsTerm" }, new object[] { ftsExpression });

            if (data.ResultSet.Length == 0) return Array.Empty<SearchHitDto>();

            var hits = new List<SearchHitDto>(data.ResultSet[0].Rows.Length);
            foreach (var row in data.ResultSet[0].Rows)
            {
                if (row.Values.Length < 3 || row.Values[0] is null) continue;

                var vsopTunnus = Convert.ToInt32(row.Values[0]);
                var tenants    = row.Values[1] as string;
                var addresses  = row.Values[2] as string;

                hits.Add(new SearchHitDto(
                    Id:       vsopTunnus.ToString(CultureInfo.InvariantCulture),
                    Title:    $"Sopimus {vsopTunnus}",
                    Meta:     JoinMeta(TruncateMeta(tenants, 60), TruncateMeta(addresses, 60)),
                    Icon:     "doc",
                    Navigate: null));
            }

            logger.LogInformation(
                "Search Contracts FTS '{Needle}' (term '{Term}', table '{Table}') returned {Count} hits",
                needle, ftsExpression, qualifiedTable, hits.Count);
            return hits;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex,
                "Search FTS for Contracts failed for {Needle} — returning empty group",
                needle);
            return Array.Empty<SearchHitDto>();
        }
    }

    // -- helpers ----------------------------------------------------------

    private static string JoinMeta(params string?[] parts) =>
        string.Join(" · ", parts.Where(p => !string.IsNullOrWhiteSpace(p)).Select(p => p!.Trim()));

    private static string? TruncateMeta(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var trimmed = value.Trim();
        return trimmed.Length <= maxLength ? trimmed : trimmed[..(maxLength - 1)] + "…";
    }

    private static bool MatchAction(SearchHitDto h, string needle) =>
        h.Title.Contains(needle, StringComparison.CurrentCultureIgnoreCase) ||
        h.Meta.Contains(needle, StringComparison.CurrentCultureIgnoreCase);

    /// <summary>
    /// Canonical group order (SCREENS.md §05). The frontend renders in
    /// this sequence regardless of how groups were populated. Vapaat
    /// huoneistot leads (most likely intent: scan available rentals),
    /// Huoneistot lands as the last data group before the static
    /// action catalogue.
    /// </summary>
    private static readonly string[] CanonicalOrder =
    [
        SearchGroupIds.Units,
        SearchGroupIds.People,
        SearchGroupIds.Contracts,
        SearchGroupIds.Apartments,
        SearchGroupIds.Actions,
    ];

    /// <summary>
    /// Static catalogue of in-app commands. Not FTS-indexed — they're
    /// a closed set of UI actions, not entities. Substring match on
    /// title or meta drives the inclusion.
    /// </summary>
    private static readonly IReadOnlyList<SearchHitDto> ACTIONS = new SearchHitDto[]
    {
        new("action-new-offer", "Tee uusi tarjous",
            "Aloita tyhjältä lomakkeelta", "plus", null),
        new("action-new-visit", "Varaa esittely",
            "Sovi aika hakijan kanssa", "event", null),
        new("action-new-task", "Lisää uusi tehtävä",
            "Luo Tehtava xVasuSecuritySystemUser:lle", "check", "/tasks"),
    };
}
