using System.Security.Claims;
using DevExpress.Data.Filtering;
using xVasu.Data.Security;

namespace mVasu.Api.Authentication;

public static class EmailResolver
{
    private static readonly string[] DomainVariants =
    [
        "kojamo.fi",
        "lumo.fi",
        "kojamo.onmicrosoft.com",
    ];

    public static string? ResolveEmail(ClaimsPrincipal principal)
    {
        if (principal.Identity?.IsAuthenticated != true)
        {
            return null;
        }

        var name = principal.Identity.Name?.ToLowerInvariant();
        if (!string.IsNullOrEmpty(name) && name.Contains('@'))
        {
            return name;
        }

        var emailClaim = principal.FindFirst(ClaimTypes.Email)
            ?? principal.FindFirst("email")
            ?? principal.FindFirst("preferred_username")
            ?? principal.FindFirst("upn");

        return emailClaim?.Value.ToLowerInvariant();
    }

    public static IReadOnlyList<string> BuildEmailVariants(string email)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(email);

        var atIndex = email.IndexOf('@');
        if (atIndex <= 0)
        {
            return [email.ToLowerInvariant()];
        }

        var localPart = email[..atIndex].ToLowerInvariant();
        var lower = email.ToLowerInvariant();

        var variants = new List<string>(DomainVariants.Length + 1);
        foreach (var domain in DomainVariants)
        {
            variants.Add($"{localPart}@{domain}");
        }

        if (!variants.Contains(lower))
        {
            variants.Add(lower);
        }

        return variants;
    }

    /// <summary>
    /// Builds the XPO criteria that locates an active mVasu user by their
    /// email variants. Replicates the legacy mVasu CustomAuthenticationProvider:
    /// vvoad-prefix UserName match for Kojamo AD users, exact email match for
    /// @kojamo.onmicrosoft.com guest tenants, mVasuEnabled and IsActive in both.
    /// </summary>
    public static CriteriaOperator BuildUserCriteria(IReadOnlyList<string> userEmailList, string email)
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
