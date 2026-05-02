using System.Security.Claims;
using mVasu.Api.Contracts;

namespace mVasu.Api.Authentication;

public interface IUserSettingsService
{
    Task<UserProfileDto?> UpdateAsync(
        ClaimsPrincipal principal,
        UpdateSettingsDto dto,
        CancellationToken cancellationToken = default);

    Task<UserProfileDto?> UpdateLocationConsentAsync(
        ClaimsPrincipal principal,
        bool consent,
        CancellationToken cancellationToken = default);

    Task<RecordLocationResult> RecordLocationAsync(
        ClaimsPrincipal principal,
        UserLocationDto location,
        CancellationToken cancellationToken = default);
}

public enum RecordLocationResult
{
    Recorded,
    UserNotProvisioned,
    ConsentNotGranted,
}
