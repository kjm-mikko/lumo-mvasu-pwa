namespace mVasu.Api.Contracts;

/// <summary>
/// Distinct values across the user's visible Tiskilista rows for the
/// dimensions the PWA exposes as multi-select filter dropdowns.
/// Scope: <c>UserAreaScope</c> (BranchCode), no other filters applied —
/// the dropdown should offer every value the user can ever see.
/// </summary>
public sealed record TiskilistaDistinctValuesDto(
    IReadOnlyList<string> Lajit,
    IReadOnlyList<string> Tyypit,
    IReadOnlyList<string> Kunnat,
    IReadOnlyList<string> Kaupunginosat,
    IReadOnlyList<string> Sopimustilat,
    IReadOnlyList<string> Isannoitsijat,
    IReadOnlyList<string> Tilat);
