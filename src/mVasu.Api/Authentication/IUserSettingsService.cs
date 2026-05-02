using System.Security.Claims;
using mVasu.Api.Contracts;

namespace mVasu.Api.Authentication;

public interface IUserSettingsService
{
    Task<UserProfileDto?> UpdateAsync(
        ClaimsPrincipal principal,
        UpdateSettingsDto dto,
        CancellationToken cancellationToken = default);
}
