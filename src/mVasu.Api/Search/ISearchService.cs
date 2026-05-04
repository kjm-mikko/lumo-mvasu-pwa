using System.Security.Claims;
using mVasu.Api.Contracts;

namespace mVasu.Api.Search;

/// <summary>
/// Long-tail global search across XAF entity types and a static action
/// catalogue. BACKEND.md §5 + SCREENS.md §05.
/// </summary>
public interface ISearchService
{
    Task<SearchResponseDto> SearchAsync(
        ClaimsPrincipal principal,
        SearchQueryParameters query,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Bound from the <c>GET /api/search</c> query string.
/// </summary>
/// <param name="Q">
///   Free-text search. Min length 2 — shorter inputs return an empty
///   result set instead of every row.
/// </param>
/// <param name="Limit">
///   Max hits per group. Defaults to 5 (BACKEND.md §5: <c>?limit=5</c>).
/// </param>
public sealed record SearchQueryParameters(
    string? Q,
    int Limit);
