using System.Security.Claims;

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
}
