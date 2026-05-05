using System.Security.Claims;
using mVasu.Api.Contracts;

namespace mVasu.Api.Tiskilista;

public interface ITiskilistaQueryService
{
    Task<TiskilistaPageDto?> ListAsync(
        ClaimsPrincipal principal,
        TiskilistaListQuery query,
        CancellationToken cancellationToken = default);

    Task<TiskilistaDetailDto?> GetAsync(
        ClaimsPrincipal principal,
        Guid id,
        CancellationToken cancellationToken = default);

    Task<TiskilistaDistinctValuesDto?> GetDistinctValuesAsync(
        ClaimsPrincipal principal,
        CancellationToken cancellationToken = default);
}
