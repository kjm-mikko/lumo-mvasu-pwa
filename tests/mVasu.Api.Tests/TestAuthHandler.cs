using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace mVasu.Api.Tests;

internal sealed class TestAuthHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string SchemeName = "Test";

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue("X-Test-User", out var userHeader))
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        var email = userHeader.ToString();
        var includeScope = !Request.Headers.ContainsKey("X-Test-NoScope");

        var claims = new List<Claim>
        {
            new(ClaimTypes.Name, email),
            new("preferred_username", email),
            new("name", "Mikko Nieminen"),
        };

        if (includeScope)
        {
            claims.Add(new("scp", "access_as_user"));
        }

        var identity = new ClaimsIdentity(claims, SchemeName);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, SchemeName);

        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}
