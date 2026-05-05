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
