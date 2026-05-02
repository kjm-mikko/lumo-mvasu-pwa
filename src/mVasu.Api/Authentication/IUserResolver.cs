using System.Security.Claims;
using mVasu.Api.Contracts;

namespace mVasu.Api.Authentication;

public interface IUserResolver
{
    Task<UserProfileDto?> ResolveAsync(ClaimsPrincipal principal, CancellationToken cancellationToken = default);
}
