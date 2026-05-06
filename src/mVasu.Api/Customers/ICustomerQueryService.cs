using System.Security.Claims;
using mVasu.Api.Contracts;

namespace mVasu.Api.Customers;

/// <summary>
/// Asiakkaat browser. Projects from <c>xVasu.Data.Asma.Asiakas</c> (and
/// its concrete subtypes <c>Henkilo</c> / <c>Yritys</c> /
/// <c>Yhteyshenkilo</c>) with related-entity counts aggregated from
/// Hakemus, SopimusVaraus, Sopimus and DirectRentalCustomerInspection.
/// Row-level visibility comes from the XPO PermissionPolicy of the
/// authenticated user's session.
/// </summary>
public interface ICustomerQueryService
{
    Task<CustomersResponseDto> ListAsync(
        ClaimsPrincipal principal,
        CustomerQueryParameters query,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the full row for a single Asiakas (Henkilo / Yritys /
    /// Yhteyshenkilo) by AsiakasNumero, or null if the row doesn't
    /// exist or the caller lacks permission to see it. The wire shape
    /// reuses <see cref="CustomerDto"/> — the detail view today shows
    /// the same fields the list row carries; specialised fields will
    /// land in a dedicated DetailDto when we need them.
    /// </summary>
    Task<CustomerDto?> GetAsync(
        ClaimsPrincipal principal,
        int asiakasNumero,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Bound from the <c>GET /api/customers</c> query string. Matching
/// semantics: <see cref="Search"/> matches name + address + city +
/// phone + email + Y-tunnus + parentCompanyName (case-insensitive).
/// <see cref="Type"/>, <see cref="Relation"/> and <see cref="City"/>
/// are independent filters combined via AND.
/// </summary>
public sealed record CustomerQueryParameters(
    string? Search,
    string? Type,
    string? Relation,
    string? City);
