using System.Globalization;
using System.Security.Claims;
using DevExpress.ExpressApp;
using DevExpress.ExpressApp.Xpo;
using DevExpress.Xpo;
using DevExpress.Xpo.DB;
using mVasu.Api.Common;
using mVasu.Api.Contracts;
using xVasu.Data.Asma;

namespace mVasu.Api.Search;

/// <summary>
/// Long-tail global search. The "Asukkaat" (People) group runs against
/// the live <c>fts_Asiakas</c> SQL Server fulltext catalog via the same
/// <see cref="FtsExpressionBuilder"/> the customer list uses. The other
/// three groups (Kohteet / Sopimukset / Toiminnot) still serve a static
/// fixture — they'll move to FTS in subsequent iterations.
/// </summary>
public sealed class SearchService(
    IObjectSpaceProvider objectSpaceProvider,
    ILogger<SearchService> logger) : ISearchService
{
    private const int MinQueryLength = 2;

    /// <summary>
    /// Server-side cap on the People FTS row count. Five hits is the
    /// default frontend limit; capping at 50 leaves headroom for the
    /// caller asking for more without unbounded payloads.
    /// </summary>
    private const int PeopleFtsCap = 50;

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

        var limit = query.Limit > 0 ? query.Limit : 5;
        var groups = new List<SearchGroupDto>();

        // Live FTS for People — returns to the empty-state-friendly
        // empty list on any error so the rest of the search response
        // still renders.
        var peopleHits = LoadPeopleHits(needle, limit);
        if (peopleHits.Count > 0)
        {
            groups.Add(new SearchGroupDto(SearchGroupIds.People, "Asukkaat", peopleHits));
        }

        // MOCK groups — Kohteet / Sopimukset / Toiminnot. These will
        // move to FTS next: t_tiskilista for units, VSOPIMUS for
        // contracts. Actions stays static (catalog of in-app commands).
        foreach (var (id, label) in MockGroupOrder)
        {
            var hits = MOCK
                .Where(h => h.Group == id)
                .Where(h => Match(h, needle))
                .Take(limit)
                .Select(h => new SearchHitDto(h.Id, h.Title, h.Meta, h.Icon, h.Navigate))
                .ToList();

            if (hits.Count > 0)
            {
                groups.Add(new SearchGroupDto(id, label, hits));
            }
        }

        // Order groups so People (now live data) appears in the same
        // canonical slot the frontend expects: Units, People, Contracts,
        // Actions. The People group was added first above to favour the
        // live-data path; we re-sort to the SCREENS.md §05 order here.
        groups = groups
            .OrderBy(g => Array.IndexOf(CanonicalOrder, g.Id))
            .ToList();

        return Task.FromResult(new SearchResponseDto(groups));
    }

    /// <summary>
    /// Single-round-trip People search. Combines the FTS pre-pass and
    /// the row-projection into one SELECT — the columns we need for
    /// <see cref="SearchHitDto"/> (AsiakasNumero, SukuNimi, KatuOsoite,
    /// PostiToimiPaikka) all live on the Asiakas base table that backs
    /// the <c>fts_Asiakas</c> catalog, so we don't need a second query
    /// to enrich the hit.
    /// </summary>
    /// <remarks>
    /// <para>SukuNimi covers all three subtypes (Henkilö surname,
    /// Yritys company name via the Yritysnimi alias, Yhteyshenkilö
    /// surname) since they share the underlying column. The detail
    /// page populates EtuNimi and the per-type DisplayName when the
    /// user clicks through; surface here is intentionally minimal so
    /// the search overlay stays sub-100ms.</para>
    ///
    /// <para>Falls back to an empty list on any failure (FTS catalog
    /// missing, table renamed, etc.) — the rest of the search response
    /// still renders rather than 500-ing the entire overlay.</para>
    /// </remarks>
    private IReadOnlyList<SearchHitDto> LoadPeopleHits(string needle, int limit)
    {
        IObjectSpace? os = null;
        try
        {
            var ftsExpression = FtsExpressionBuilder.Build(needle);
            if (ftsExpression is null) return Array.Empty<SearchHitDto>();

            os = objectSpaceProvider.CreateObjectSpace();
            var session = ((XPObjectSpace)os).Session;

            var classInfo = session.GetClassInfo(typeof(Asiakas));
            var qualifiedTable = XpoTableNameResolver.Qualified(classInfo.TableName);

            // Cap at the lesser of the caller's limit and the safety
            // ceiling — the SQL TOP clause is a hard cut, so we
            // shouldn't ever return more rows than the caller asked for.
            var effectiveTop = Math.Min(limit, PeopleFtsCap);

            var sql =
                $"SELECT TOP {effectiveTop} " +
                "[AsiakasNumero], [SukuNimi], [KatuOsoite], [PostiToimiPaikka] " +
                $"FROM {qualifiedTable} " +
                "WHERE [GCRecord] IS NULL " +
                "  AND CONTAINS(([EtuNimi], [SukuNimi], [KatuOsoite], [Email], [Gsm]), @ftsTerm)";

            SelectedData data = session.ExecuteQuery(sql,
                new[] { "@ftsTerm" },
                new object[] { ftsExpression });

            if (data.ResultSet.Length == 0)
            {
                logger.LogInformation(
                    "Search People FTS '{Needle}' (term '{Term}', table '{Table}') returned no result set",
                    needle, ftsExpression, qualifiedTable);
                return Array.Empty<SearchHitDto>();
            }

            var hits = new List<SearchHitDto>(data.ResultSet[0].Rows.Length);
            foreach (var row in data.ResultSet[0].Rows)
            {
                if (row.Values.Length < 4 || row.Values[0] is null) continue;

                var asiakasNumero = Convert.ToInt32(row.Values[0]);
                var sukuNimi      = row.Values[1] as string;
                var katuOsoite    = row.Values[2] as string;
                var paikka        = row.Values[3] as string;

                var title = string.IsNullOrWhiteSpace(sukuNimi)
                    ? "(nimetön)"
                    : sukuNimi.Trim();

                hits.Add(new SearchHitDto(
                    Id:       asiakasNumero.ToString(CultureInfo.InvariantCulture),
                    Title:    title,
                    Meta:     BuildPeopleMeta(katuOsoite, paikka),
                    Icon:     "user",
                    Navigate: $"/customers/{asiakasNumero}"));
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
        finally
        {
            os?.Dispose();
        }
    }

    private static string BuildPeopleMeta(string? katuOsoite, string? paikka)
    {
        var parts = new List<string>(2);
        if (!string.IsNullOrWhiteSpace(katuOsoite)) parts.Add(katuOsoite.Trim());
        if (!string.IsNullOrWhiteSpace(paikka))     parts.Add(paikka.Trim());
        return parts.Count == 0 ? string.Empty : string.Join(" · ", parts);
    }

    private static bool Match(MockHit h, string needle) =>
        h.Title.Contains(needle, StringComparison.CurrentCultureIgnoreCase) ||
        h.Meta.Contains(needle, StringComparison.CurrentCultureIgnoreCase);

    /// <summary>
    /// Canonical group order (SCREENS.md §05). The frontend renders
    /// groups in this sequence regardless of the order we built them.
    /// </summary>
    private static readonly string[] CanonicalOrder =
    [
        SearchGroupIds.Units,
        SearchGroupIds.People,
        SearchGroupIds.Contracts,
        SearchGroupIds.Actions,
    ];

    /// <summary>Mock-backed groups still rendered from <see cref="MOCK"/>.</summary>
    private static readonly (string Id, string Label)[] MockGroupOrder =
    [
        (SearchGroupIds.Units,     "Kohteet"),
        (SearchGroupIds.Contracts, "Sopimukset"),
        (SearchGroupIds.Actions,   "Toiminnot"),
    ];

    private sealed record MockHit(
        string Id,
        string Group,
        string Title,
        string Meta,
        string Icon,
        string? Navigate);

    private static readonly IReadOnlyList<MockHit> MOCK = new MockHit[]
    {
        // Units (XAF Asuinhuoneisto / Tiskilista)
        new("unit-1", SearchGroupIds.Units, "Mannerheimintie 12 A 4",
            "2 h · 47 m² · vapaa 1.7.", "home", "/tiskilista"),
        new("unit-2", SearchGroupIds.Units, "Mannerheimintie 12 B 7",
            "3 h · 68 m² · varattu", "home", null),
        new("unit-3", SearchGroupIds.Units, "Aleksanterinkatu 12",
            "4 h+s · 95 m² · vapaa 15.6.", "home", null),
        new("unit-4", SearchGroupIds.Units, "Maauunintie 23 A 2, 01450 Vantaa",
            "1 h+kk · 32 m² · esittely huomenna 14:00", "home", null),
        new("unit-5", SearchGroupIds.Units, "Asemakuja 1 B 69, 02770 Espoo",
            "2 h+k · 54 m² · varattu", "home", null),
        new("unit-6", SearchGroupIds.Units, "Vänrikinkatu 2",
            "2 h+k · 48,5 m² · sopimus odottaa", "home", null),

        // Contracts (XAF Sopimus / Tarjous)
        new("contract-1", SearchGroupIds.Contracts, "Tarjous SOP-PLACEHOLDER · Vänrikinkatu 2",
            "Allekirjoitusta odottaa · 3 pv", "doc", null),
        new("contract-2", SearchGroupIds.Contracts, "Sopimus 100029",
            "Voimassa · Aleksanterinkatu 12 · Päättyy 31.1.2027", "doc", null),
        new("contract-3", SearchGroupIds.Contracts, "Irtisanominen 30.4.",
            "Käsittelyä odottaa · Vänrikinkatu 2", "doc", null),

        // Actions (static catalogue)
        new("action-new-offer", SearchGroupIds.Actions, "Tee uusi tarjous",
            "Aloita tyhjältä lomakkeelta", "plus", null),
        new("action-new-visit", SearchGroupIds.Actions, "Varaa esittely",
            "Sovi aika hakijan kanssa", "event", null),
        new("action-new-task", SearchGroupIds.Actions, "Lisää uusi tehtävä",
            "Luo Tehtava xVasuSecuritySystemUser:lle", "check", "/tasks"),
    };
}
