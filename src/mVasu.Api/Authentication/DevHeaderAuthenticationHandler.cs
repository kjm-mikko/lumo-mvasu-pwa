using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace mVasu.Api.Authentication;

public static class DevHeaderAuthenticationDefaults
{
    public const string Scheme = "DevHeader";
    public const string HeaderName = "X-Dev-User";
}

public sealed class DevHeaderAuthenticationOptions : AuthenticationSchemeOptions
{
    /// <summary>
    /// Scope claim value emitted on the synthetic principal. Must match the
    /// scope required by AccessAsUserPolicy in Program.cs (default
    /// <c>access_as_user</c>) so authorization passes the same way it would
    /// for a real Azure AD JWT.
    /// </summary>
    public string Scope { get; set; } = "access_as_user";
}

/// <summary>
/// Dev-only authentication handler. Reads the email from the
/// <c>X-Dev-User</c> request header and produces an authenticated
/// principal that the existing <see cref="EmailResolver"/> +
/// <see cref="XpoEmailUserResolver"/> pipeline maps to an mVasu user.
///
/// Wired up in Program.cs only when <c>Development:DevHeaderAuth:Enabled</c>
/// is true AND the host environment is Development. Production requests
/// never reach this handler — the registration is gated at startup, not
/// at handler-evaluation time.
/// </summary>
public sealed class DevHeaderAuthenticationHandler(
    IOptionsMonitor<DevHeaderAuthenticationOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder)
    : AuthenticationHandler<DevHeaderAuthenticationOptions>(options, logger, encoder)
{
    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue(DevHeaderAuthenticationDefaults.HeaderName, out var values))
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        var raw = values.ToString().Trim();
        if (string.IsNullOrWhiteSpace(raw) || !raw.Contains('@'))
        {
            return Task.FromResult(AuthenticateResult.Fail(
                $"Invalid {DevHeaderAuthenticationDefaults.HeaderName} header — expected an email address."));
        }

        var email = raw.ToLowerInvariant();

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, email),
            new Claim(ClaimTypes.Name, email),
            new Claim(ClaimTypes.Email, email),
            new Claim("preferred_username", email),
            new Claim("scp", Options.Scope),
        };

        var identity = new ClaimsIdentity(claims, DevHeaderAuthenticationDefaults.Scheme);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, DevHeaderAuthenticationDefaults.Scheme);
        return Task.FromResult(AuthenticateResult.Success(ticket));
    }

    protected override Task HandleChallengeAsync(AuthenticationProperties properties)
    {
        Response.StatusCode = StatusCodes.Status401Unauthorized;
        Response.Headers.Append(
            "WWW-Authenticate",
            $"{DevHeaderAuthenticationDefaults.Scheme} realm=\"mVasu dev\"");
        return Task.CompletedTask;
    }
}
