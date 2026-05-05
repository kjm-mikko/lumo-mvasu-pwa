using xVasu.Data.Security;

namespace mVasu.Api.Authentication;

/// <summary>
/// Resolves the area-toimisto (branchcode) visibility scope for a given
/// user. The legacy mVasu rule: any permission code (Kayttooikeudet) of
/// "999" means "all areas, no restriction" — global override. Otherwise
/// the user is restricted to the area codes listed in AlueToimistot.
/// Both Tiskilista and Tarjous (SopimusVaraus) read this scope.
/// </summary>
public static class UserAreaScope
{
    public const string GlobalAccessCode = "999";

    public readonly record struct Resolution(bool Unrestricted, IReadOnlyList<string> Areas)
    {
        /// <summary>True when the user has at least one area visible (or
        /// is unrestricted). False means show no rows at all.</summary>
        public bool HasAccess => Unrestricted || Areas.Count > 0;
    }

    public static Resolution Resolve(xVasuSecuritySystemUser user)
    {
        var rights = user.Kayttooikeudet;
        if (rights is not null && rights.Contains(GlobalAccessCode))
        {
            return new Resolution(Unrestricted: true, Array.Empty<string>());
        }

        var areas = (user.AlueToimistot ?? new List<string>())
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        return new Resolution(Unrestricted: false, areas);
    }
}
