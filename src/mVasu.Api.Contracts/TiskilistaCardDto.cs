namespace mVasu.Api.Contracts;

/// <summary>
/// Compact card view for the Tiskilista list. Field types mirror the XPO
/// schema exactly: vuokra is double, neliot is float, kerros is a free-form
/// string ("2/4"), so the JSON shape stays lossless.
/// </summary>
public sealed record TiskilistaCardDto(
    Guid Id,
    string Osoite,
    string? Tyyppi,
    double? Vuokra,
    DateTimeOffset? Vapautuu,
    float? Neliot,
    string? Kerros,
    string? Kerroksia,
    string? Tila,
    string? SopimusTila,
    string? Kunta,
    string? Kaupunginosa,
    bool LumoFi,
    bool Vuokraovi,
    bool OnKuvausTarve,
    bool Hissi,
    bool Parveke,
    bool Sauna,
    double? Latitude,
    double? Longitude,
    double? DistanceKm);
