using System.Security.Claims;
using mVasu.Api.Authentication;

namespace mVasu.Api.Tests;

/// <summary>
/// Tests the dev-only impersonation override on
/// <see cref="EmailResolver"/>. Static state is process-wide so this
/// class shares an xunit collection with <see cref="EmailResolverTests"/>
/// — collection membership prevents the two from running in parallel
/// and leaking the static field across each other. Per-test cleanup is
/// also belt-and-braces in <see cref="Dispose"/>.
/// </summary>
[Collection(EmailResolverCollection.Name)]
public class EmailResolverImpersonationTests : IDisposable
{
    public void Dispose() => EmailResolver.SetDevelopmentImpersonation(null);

    [Fact]
    public void Impersonation_OnAuthenticatedPrincipal_OverridesEmail()
    {
        EmailResolver.SetDevelopmentImpersonation("test.user@kojamo.fi");

        var principal = AuthenticatedPrincipal("real.user@kojamo.fi");

        Assert.Equal("test.user@kojamo.fi", EmailResolver.ResolveEmail(principal));
    }

    [Fact]
    public void Impersonation_OnUnauthenticatedPrincipal_ReturnsNull()
    {
        EmailResolver.SetDevelopmentImpersonation("test.user@kojamo.fi");

        var anonymous = new ClaimsPrincipal(new ClaimsIdentity());

        // Auth gate: impersonation does NOT short-circuit the
        // unauthenticated path; the request still gets a 401 upstream.
        Assert.Null(EmailResolver.ResolveEmail(anonymous));
    }

    [Fact]
    public void Impersonation_NormalizesCasingAndWhitespace()
    {
        EmailResolver.SetDevelopmentImpersonation("  Test.User@Kojamo.FI  ");

        var principal = AuthenticatedPrincipal("real.user@kojamo.fi");

        Assert.Equal("test.user@kojamo.fi", EmailResolver.ResolveEmail(principal));
    }

    [Fact]
    public void Impersonation_NullOrBlank_ClearsOverride()
    {
        EmailResolver.SetDevelopmentImpersonation("test.user@kojamo.fi");
        EmailResolver.SetDevelopmentImpersonation("   ");

        var principal = AuthenticatedPrincipal("real.user@kojamo.fi");

        // Empty/whitespace clears — original principal email comes
        // through.
        Assert.Equal("real.user@kojamo.fi", EmailResolver.ResolveEmail(principal));
    }

    [Fact]
    public void NoImpersonation_FallsThroughToPrincipalClaims()
    {
        // Don't set impersonation at all.
        var principal = AuthenticatedPrincipal("real.user@kojamo.fi");

        Assert.Equal("real.user@kojamo.fi", EmailResolver.ResolveEmail(principal));
    }

    [Fact]
    public void CurrentDevelopmentImpersonation_ReflectsSetterState()
    {
        Assert.Null(EmailResolver.CurrentDevelopmentImpersonation);

        EmailResolver.SetDevelopmentImpersonation("test.user@kojamo.fi");
        Assert.Equal("test.user@kojamo.fi", EmailResolver.CurrentDevelopmentImpersonation);

        EmailResolver.SetDevelopmentImpersonation(null);
        Assert.Null(EmailResolver.CurrentDevelopmentImpersonation);
    }

    private static ClaimsPrincipal AuthenticatedPrincipal(string email)
    {
        var identity = new ClaimsIdentity(
            new[]
            {
                new Claim(ClaimTypes.Name, email),
                new Claim("preferred_username", email),
            },
            authenticationType: "Test");
        return new ClaimsPrincipal(identity);
    }
}
