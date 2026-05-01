using System.Security.Claims;
using mVasu.Api.Authentication;

namespace mVasu.Api.Tests;

public class EmailResolverTests
{
    [Fact]
    public void ResolveEmail_FromIdentityName_ReturnsLowercase()
    {
        var identity = new ClaimsIdentity([
            new Claim(ClaimTypes.Name, "Mikko.Nieminen@Kojamo.fi"),
        ], "Test");
        var principal = new ClaimsPrincipal(identity);

        var email = EmailResolver.ResolveEmail(principal);

        Assert.Equal("mikko.nieminen@kojamo.fi", email);
    }

    [Fact]
    public void ResolveEmail_FromEmailClaim_WhenNameIsNotEmail_ReturnsClaimValue()
    {
        var identity = new ClaimsIdentity([
            new Claim(ClaimTypes.Name, "Mikko Nieminen"),
            new Claim(ClaimTypes.Email, "Mikko.Nieminen@Lumo.fi"),
        ], "Test");
        var principal = new ClaimsPrincipal(identity);

        var email = EmailResolver.ResolveEmail(principal);

        Assert.Equal("mikko.nieminen@lumo.fi", email);
    }

    [Fact]
    public void ResolveEmail_FromUnauthenticatedPrincipal_ReturnsNull()
    {
        var principal = new ClaimsPrincipal(new ClaimsIdentity());

        var email = EmailResolver.ResolveEmail(principal);

        Assert.Null(email);
    }

    [Fact]
    public void BuildEmailVariants_WithKojamoEmail_ReturnsAllThreeDomains()
    {
        var variants = EmailResolver.BuildEmailVariants("mikko.nieminen@kojamo.fi");

        Assert.Equal(3, variants.Count);
        Assert.Contains("mikko.nieminen@kojamo.fi", variants);
        Assert.Contains("mikko.nieminen@lumo.fi", variants);
        Assert.Contains("mikko.nieminen@kojamo.onmicrosoft.com", variants);
    }

    [Fact]
    public void BuildEmailVariants_WithUnknownDomain_AppendsOriginalToVariants()
    {
        var variants = EmailResolver.BuildEmailVariants("partner@external.com");

        Assert.Contains("partner@kojamo.fi", variants);
        Assert.Contains("partner@lumo.fi", variants);
        Assert.Contains("partner@kojamo.onmicrosoft.com", variants);
        Assert.Contains("partner@external.com", variants);
    }

    [Fact]
    public void BuildEmailVariants_NormalizesCase()
    {
        var variants = EmailResolver.BuildEmailVariants("Mikko.Nieminen@Kojamo.FI");

        Assert.All(variants, v => Assert.Equal(v, v.ToLowerInvariant()));
        Assert.Contains("mikko.nieminen@kojamo.fi", variants);
    }
}
