using System.Security.Claims;
using mVasu.Api.Contracts;

namespace mVasu.Api.Search;

/// <summary>
/// Phase-1 mock implementation of <see cref="ISearchService"/>. Mirrors
/// the synthetic dataset the PWA used to ship in QuickSearchService —
/// same entity rows, same action catalogue. Phase 2 will replace
/// <see cref="MOCK"/> with a real Postgres tsvector / pg_trgm fuzzy
/// search across the four entity tables.
/// </summary>
public sealed class SearchService : ISearchService
{
    private const int MinQueryLength = 2;

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

        foreach (var (id, label) in GroupOrder)
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

        return Task.FromResult(new SearchResponseDto(groups));
    }

    private static bool Match(MockHit h, string needle) =>
        h.Title.Contains(needle, StringComparison.CurrentCultureIgnoreCase) ||
        h.Meta.Contains(needle, StringComparison.CurrentCultureIgnoreCase);

    private static readonly (string Id, string Label)[] GroupOrder =
    [
        (SearchGroupIds.Units,     "Kohteet"),
        (SearchGroupIds.People,    "Asukkaat"),
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

        // People (XAF Henkilo / Hakija)
        new("person-1", SearchGroupIds.People, "Asiakas_001",
            "Hakija · Vänrikinkatu 2", "user", null),
        new("person-2", SearchGroupIds.People, "Asiakas_002",
            "Hakija · Mannerheimintie 12 A 4", "user", null),
        new("person-3", SearchGroupIds.People, "Asiakas_003",
            "Asukas · Vänrikinkatu 2", "user", null),
        new("person-4", SearchGroupIds.People, "Asiakas_004",
            "Päähakija · Asemakuja 1 B 69", "user", null),
        new("person-5", SearchGroupIds.People, "Asiakas_005",
            "Liidi · max 555 €/kk · Lappeenranta", "user", null),

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
