using System.Security.Claims;
using DevExpress.Data.Filtering;
using DevExpress.ExpressApp;
using mVasu.Api.Contracts;
using mVasu.Api.Domain;
using xVasu.Data.Security;

namespace mVasu.Api.Authentication;

/// <summary>
/// Persists per-user mVasu settings (PreferredName / Theme / Language) into
/// the MVasuUserSettings table. Locates the xVasu user with the same criteria
/// as <see cref="XpoEmailUserResolver"/>, then patches or creates the
/// associated settings row in a single object space transaction.
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
        var email = EmailResolver.ResolveEmail(principal);
        if (string.IsNullOrEmpty(email))
        {
            logger.LogWarning("Settings update rejected — principal had no resolvable email");
            return Task.FromResult<UserProfileDto?>(null);
        }

        var variants = EmailResolver.BuildEmailVariants(email);
        using var os = objectSpaceProvider.CreateObjectSpace();

        var user = os.FindObject<xVasuSecuritySystemUser>(EmailResolver.BuildUserCriteria(variants, email));
        if (user is null)
        {
            logger.LogInformation("Settings update rejected — user {Email} not provisioned", email);
            return Task.FromResult<UserProfileDto?>(null);
        }

        var settings = os.FindObject<MVasuUserSettings>(
            CriteriaOperator.FromLambda<MVasuUserSettings>(s => s.User.Oid == user.Oid));

        if (settings is null)
        {
            settings = os.CreateObject<MVasuUserSettings>();
            settings.User = user;
        }

        settings.PreferredName = string.IsNullOrWhiteSpace(dto.PreferredName) ? null : dto.PreferredName.Trim();
        settings.Theme = dto.Theme;
        settings.Language = dto.Language;

        os.CommitChanges();

        var displayName = principal.FindFirst("name")?.Value
            ?? user.Kokonimi
            ?? user.UserName
            ?? user.Email
            ?? email;

        var profile = new UserProfileDto(
            Id: user.Oid,
            Email: user.Email ?? email,
            DisplayName: displayName,
            PreferredName: settings.PreferredName,
            Theme: settings.Theme,
            Language: settings.Language,
            LocationConsent: settings.LocationConsent);

        return Task.FromResult<UserProfileDto?>(profile);
    }
}
