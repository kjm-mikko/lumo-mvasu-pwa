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

    /// <summary>
    /// Process-wide impersonation override populated only in Development
    /// from the <c>Development:ImpersonateEmail</c> configuration value
    /// (see <c>Program.cs</c>). When set, every authenticated request
    /// resolves to this email regardless of the principal's claims —
    /// used to test as a different mVasu user without re-logging into
    /// Azure AD locally. Production guard is the env check at startup,
    /// not this field.
    /// </summary>
    private static string? _developmentImpersonation;

    /// <summary>
    /// Sets the dev-only impersonation email. Caller is responsible for
    /// gating on <see cref="IHostEnvironment.IsDevelopment"/> and
    /// surfacing a warning at startup so it's hard to miss when active.
    /// </summary>
    public static void SetDevelopmentImpersonation(string? email)
    {
        _developmentImpersonation = string.IsNullOrWhiteSpace(email)
            ? null
            : email.Trim().ToLowerInvariant();
    }

    /// <summary>
    /// Returns the currently active development impersonation email, or
    /// null when impersonation is off. Useful for /api/me-style probes
    /// that want to surface "you're impersonating X" in their response.
    /// </summary>
    public static string? CurrentDevelopmentImpersonation => _developmentImpersonation;

    public static string? ResolveEmail(ClaimsPrincipal principal)
    {
        if (principal.Identity?.IsAuthenticated != true)
        {
            return null;
        }

        // Dev impersonation runs only after the principal has been
        // authenticated by the standard JWT/Test handler — this keeps
        // unauthenticated callers on the 401 path. The override itself
        // is gated to Development at registration time.
        if (_developmentImpersonation is not null)
        {
            return _developmentImpersonation;
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
