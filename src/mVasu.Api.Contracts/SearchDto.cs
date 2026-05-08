namespace mVasu.Api.Contracts;

/// <summary>
/// Long-tail global search response. BACKEND.md §5 expressed the shape
/// as <c>{ units, people, contracts, actions }</c>; we use a uniform
/// <see cref="Groups"/> array instead so the server can add categories
/// (e.g. Remontit, Liidit) without breaking the wire — the frontend
/// already renders the result with a grouped <c>dx-list</c> driven by
/// <see cref="SearchGroupDto.Id"/> and <see cref="SearchGroupDto.Label"/>.
/// </summary>
public sealed record SearchResponseDto(
    IReadOnlyList<SearchGroupDto> Groups);

/// <summary>
/// One result section in the quick-search overlay.
/// </summary>
public sealed record SearchGroupDto(
    /// <summary>One of <see cref="SearchGroupIds"/>.</summary>
    string Id,
    string Label,
    IReadOnlyList<SearchHitDto> Hits);

/// <summary>
/// Single search hit. The frontend handles the highlight client-side
/// (so server can stream raw <see cref="Title"/> / <see cref="Meta"/>
/// without a tokenisation pass). <see cref="Navigate"/> is the route
/// the row jumps to when tapped — for entity hits it's a deep-link
/// to the detail view, for action hits it's a feature route.
/// </summary>
public sealed record SearchHitDto(
    string Id,
    string Title,
    string Meta,
    /// <summary>DevExtreme icon name shown to the left of the row.</summary>
    string Icon,
    /// <summary>Internal route. Null falls back to a "Tulossa"-toast in the client.</summary>
    string? Navigate);

/// <summary>Canonical group identifiers — see SCREENS.md §05.</summary>
public static class SearchGroupIds
{
    public const string Apartments = "apartments";  // Huoneistot (all residential, t_Huoneisto FTS)
    public const string Units      = "units";       // Vapaat huoneistot (t_tiskilista — vacant only)
    public const string People     = "people";      // Asukkaat
    public const string Contracts  = "contracts";   // Sopimukset
    public const string Actions    = "actions";     // Toiminnot
}
