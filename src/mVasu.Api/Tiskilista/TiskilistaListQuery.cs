namespace mVasu.Api.Tiskilista;

public sealed record TiskilistaListQuery(
    string? Search = null,
    string? Status = null,
    string Scope = "omat",
    string SortBy = "vapautuu",
    double? UserLat = null,
    double? UserLon = null,
    int Page = 1,
    int PageSize = 20)
{
    public const int MinPageSize = 1;
    public const int MaxPageSize = 100;

    public static readonly IReadOnlySet<string> ValidScopes =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "omat", "kaikki" };

    public static readonly IReadOnlySet<string> ValidSortBy =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "vapautuu", "osoite", "vuokra", "distance" };
}
