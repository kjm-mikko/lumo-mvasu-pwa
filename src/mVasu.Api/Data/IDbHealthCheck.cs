using mVasu.Api.Contracts;

namespace mVasu.Api.Data;

public interface IDbHealthCheck
{
    Task<DbHealthCheckDto> CheckAsync(CancellationToken cancellationToken = default);
}
