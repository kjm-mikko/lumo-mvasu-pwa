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
    IReadOnlyList<TiskilistaKuntaKaupunginosaDto> KaupunginosatByKunta,
    IReadOnlyList<string> Sopimustilat,
    IReadOnlyList<string> Isannoitsijat,
    IReadOnlyList<string> Markkinoijat,
    IReadOnlyList<string> Tilat);

/// <summary>
/// Distinct (kunta, kaupunginosa) pair so the PWA can scope the
/// Kaupunginosa multi-select dropdown to the kunta the user already
/// selected. Sent in addition to the flat Kaupunginosat list so the
/// "no kunta selected" case still has every district available.
/// </summary>
public sealed record TiskilistaKuntaKaupunginosaDto(
    string Kunta,
    string Kaupunginosa);
