using System.Security.Claims;
using mVasu.Api.Contracts;

namespace mVasu.Api.Authentication;

/// <summary>
/// Phase 3 placeholder: returns a hard-coded profile for any authenticated principal.
/// Phase 5 replaces this with an XPO-backed resolver against xVasuSecuritySystemUser.
/// </summary>
public sealed class StaticTestUserResolver(ILogger<StaticTestUserResolver> logger) : IUserResolver
{
    private static readonly Guid TestUserId = Guid.Parse("a1b2c3d4-e5f6-4789-abcd-1234567890ab");

    public Task<UserProfileDto?> ResolveAsync(ClaimsPrincipal principal, CancellationToken cancellationToken = default)
    {
        var email = EmailResolver.ResolveEmail(principal);
        if (string.IsNullOrEmpty(email))
        {
            logger.LogWarning("Authenticated principal had no resolvable email claim");
            return Task.FromResult<UserProfileDto?>(null);
        }

        var variants = EmailResolver.BuildEmailVariants(email);
        logger.LogDebug("Resolved email {Email} with {Count} variants {Variants}", email, variants.Count, variants);

        var displayName = principal.FindFirst("name")?.Value
            ?? principal.FindFirst(ClaimTypes.Name)?.Value
            ?? email;

        var profile = new UserProfileDto(
            Id: TestUserId,
            Email: email,
            DisplayName: displayName,
            PreferredName: null,
            Theme: "light",
            Language: "fi",
            LocationConsent: false);

        return Task.FromResult<UserProfileDto?>(profile);
    }
}
