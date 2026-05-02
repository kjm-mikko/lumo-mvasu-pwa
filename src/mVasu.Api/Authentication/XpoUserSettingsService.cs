using System.Security.Claims;
using DevExpress.Data.Filtering;
using DevExpress.ExpressApp;
using mVasu.Api.Contracts;
using mVasu.Api.Domain;
using xVasu.Data.Security;

namespace mVasu.Api.Authentication;

/// <summary>
/// Persists per-user mVasu state (settings, location consent, last location)
/// into the MVasuUserSettings table. Shares user-lookup criteria with
/// <see cref="XpoEmailUserResolver"/> via <see cref="EmailResolver.BuildUserCriteria"/>.
/// </summary>
public sealed class XpoUserSettingsService(
    IObjectSpaceProvider objectSpaceProvider,
    ILogger<XpoUserSettingsService> logger) : IUserSettingsService
{
    public Task<UserProfileDto?> UpdateAsync(
        ClaimsPrincipal principal,
        UpdateSettingsDto dto,
        CancellationToken cancellationToken = default)
    {
        return ApplyAsync(principal, "Settings update", (settings, _) =>
        {
            settings.PreferredName = string.IsNullOrWhiteSpace(dto.PreferredName)
                ? null
                : dto.PreferredName.Trim();
            settings.Theme = dto.Theme;
            settings.Language = dto.Language;
        });
    }

    public Task<UserProfileDto?> UpdateLocationConsentAsync(
        ClaimsPrincipal principal,
        bool consent,
        CancellationToken cancellationToken = default)
    {
        return ApplyAsync(principal, "Consent update", (settings, _) =>
        {
            settings.LocationConsent = consent;

            // When the user revokes consent we clear the last known location
            // so we never serve stale data captured under the prior consent.
            if (!consent)
            {
                settings.LastLocationLatitude = null;
                settings.LastLocationLongitude = null;
                settings.LastLocationAccuracyMeters = null;
                settings.LastLocationRecordedAt = null;
            }
        });
    }

    public Task<RecordLocationResult> RecordLocationAsync(
        ClaimsPrincipal principal,
        UserLocationDto location,
        CancellationToken cancellationToken = default)
    {
        var email = EmailResolver.ResolveEmail(principal);
        if (string.IsNullOrEmpty(email))
        {
            logger.LogWarning("Location record rejected — principal had no resolvable email");
            return Task.FromResult(RecordLocationResult.UserNotProvisioned);
        }

        var variants = EmailResolver.BuildEmailVariants(email);
        using var os = objectSpaceProvider.CreateObjectSpace();

        var user = os.FindObject<xVasuSecuritySystemUser>(EmailResolver.BuildUserCriteria(variants, email));
        if (user is null)
        {
            logger.LogInformation("Location record rejected — user {Email} not provisioned", email);
            return Task.FromResult(RecordLocationResult.UserNotProvisioned);
        }

        var settings = LoadOrCreateSettings(os, user);

        if (!settings.LocationConsent)
        {
            logger.LogInformation("Location record rejected — user {Email} has not granted location consent", email);
            return Task.FromResult(RecordLocationResult.ConsentNotGranted);
        }

        settings.LastLocationLatitude = location.Latitude;
        settings.LastLocationLongitude = location.Longitude;
        settings.LastLocationAccuracyMeters = location.Accuracy;
        settings.LastLocationRecordedAt = location.RecordedAt;

        os.CommitChanges();

        return Task.FromResult(RecordLocationResult.Recorded);
    }

    private Task<UserProfileDto?> ApplyAsync(
        ClaimsPrincipal principal,
        string operation,
        Action<MVasuUserSettings, xVasuSecuritySystemUser> mutate)
    {
        var email = EmailResolver.ResolveEmail(principal);
        if (string.IsNullOrEmpty(email))
        {
            logger.LogWarning("{Operation} rejected — principal had no resolvable email", operation);
            return Task.FromResult<UserProfileDto?>(null);
        }

        var variants = EmailResolver.BuildEmailVariants(email);
        using var os = objectSpaceProvider.CreateObjectSpace();

        var user = os.FindObject<xVasuSecuritySystemUser>(EmailResolver.BuildUserCriteria(variants, email));
        if (user is null)
        {
            logger.LogInformation("{Operation} rejected — user {Email} not provisioned", operation, email);
            return Task.FromResult<UserProfileDto?>(null);
        }

        var settings = LoadOrCreateSettings(os, user);
        mutate(settings, user);
        os.CommitChanges();

        return Task.FromResult<UserProfileDto?>(BuildProfile(principal, user, settings, email));
    }

    private static MVasuUserSettings LoadOrCreateSettings(IObjectSpace os, xVasuSecuritySystemUser user)
    {
        var existing = os.FindObject<MVasuUserSettings>(
            CriteriaOperator.FromLambda<MVasuUserSettings>(s => s.User.Oid == user.Oid));

        if (existing is not null)
        {
            return existing;
        }

        var created = os.CreateObject<MVasuUserSettings>();
        created.User = user;
        return created;
    }

    private static UserProfileDto BuildProfile(
        ClaimsPrincipal principal,
        xVasuSecuritySystemUser user,
        MVasuUserSettings settings,
        string fallbackEmail)
    {
        var displayName = principal.FindFirst("name")?.Value
            ?? user.Kokonimi
            ?? user.UserName
            ?? user.Email
            ?? fallbackEmail;

        return new UserProfileDto(
            Id: user.Oid,
            Email: user.Email ?? fallbackEmail,
            DisplayName: displayName,
            PreferredName: settings.PreferredName,
            Theme: settings.Theme,
            Language: settings.Language,
            LocationConsent: settings.LocationConsent);
    }
}
