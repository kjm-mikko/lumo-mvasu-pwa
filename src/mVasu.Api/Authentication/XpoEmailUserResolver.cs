using System.Security.Claims;
using DevExpress.Data.Filtering;
using DevExpress.ExpressApp;
using mVasu.Api.Contracts;
using mVasu.Api.Domain;
using xVasu.Data.Security;

namespace mVasu.Api.Authentication;

/// <summary>
/// Resolves the authenticated principal to an mVasu user via XPO, replicating
/// the criteria from the legacy mVasu CustomAuthenticationProvider:
/// vvoad-prefix UserName match for Kojamo AD users, exact email match for
/// @kojamo.onmicrosoft.com guest tenants, mVasuEnabled and IsActive in both cases.
/// </summary>
public sealed class XpoEmailUserResolver(
    IObjectSpaceProvider objectSpaceProvider,
    ILogger<XpoEmailUserResolver> logger) : IUserResolver
{
    public Task<UserProfileDto?> ResolveAsync(ClaimsPrincipal principal, CancellationToken cancellationToken = default)
    {
        var email = EmailResolver.ResolveEmail(principal);
        if (string.IsNullOrEmpty(email))
        {
            logger.LogWarning("Authenticated principal had no resolvable email claim");
            return Task.FromResult<UserProfileDto?>(null);
        }

        var variants = EmailResolver.BuildEmailVariants(email);
        using var os = objectSpaceProvider.CreateObjectSpace();

        var user = os.FindObject<xVasuSecuritySystemUser>(BuildCriteria(variants, email));
        if (user is null)
        {
            logger.LogInformation(
                "User {Email} not found in xVasuSecuritySystemUser (variants tried: {Count})",
                email, variants.Count);
            return Task.FromResult<UserProfileDto?>(null);
        }

        var settings = os.FindObject<MVasuUserSettings>(
            CriteriaOperator.FromLambda<MVasuUserSettings>(s => s.User.Oid == user.Oid));

        var displayName = principal.FindFirst("name")?.Value
            ?? user.Kokonimi
            ?? user.UserName
            ?? user.Email
            ?? email;

        var profile = new UserProfileDto(
            Id: user.Oid,
            Email: user.Email ?? email,
            DisplayName: displayName,
            PreferredName: settings?.PreferredName,
            Theme: settings?.Theme ?? "light",
            Language: settings?.Language ?? "fi",
            LocationConsent: settings?.LocationConsent ?? false);

        return Task.FromResult<UserProfileDto?>(profile);
    }

    private static CriteriaOperator BuildCriteria(IReadOnlyList<string> userEmailList, string email)
    {
        if (email.EndsWith("@kojamo.onmicrosoft.com", StringComparison.OrdinalIgnoreCase))
        {
#pragma warning disable CRR0050 // XPO CriteriaOperator.FromLambda requires == for SQL translation
            return CriteriaOperator.FromLambda<xVasuSecuritySystemUser>(
                u => userEmailList.Contains(u.Email.ToLower())
                    && u.Email.ToLower() == email
                    && u.mVasuEnabled
                    && u.IsActive);
#pragma warning restore CRR0050
        }

        return CriteriaOperator.FromLambda<xVasuSecuritySystemUser>(
            u => userEmailList.Contains(u.Email.ToLower())
                && u.UserName.ToLower().StartsWith("vvoad")
                && u.mVasuEnabled
                && u.IsActive);
    }
}
