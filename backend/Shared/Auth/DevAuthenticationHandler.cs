using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace NextStep.Shared.Auth;

/// <summary>
/// Authentication handler used ONLY in local development when Keycloak is not
/// running (Auth:Mode = "Dev"). It synthesizes the same claims the rest of the
/// application expects from a Keycloak JWT so that [Authorize] endpoints and
/// the JIT user provisioning (EnsureUserCreatedAsync) keep working unchanged.
/// Never enable this mode outside of development.
/// </summary>
public class DevAuthenticationHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    public const string SchemeName = "Dev";
    public const string DevUserId = "dev-user";
    public const string DevUserEmail = "dev@nextstep.local";

    public DevAuthenticationHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder)
        : base(options, logger, encoder)
    {
    }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, DevUserId),
            new Claim("sub", DevUserId),
            new Claim(ClaimTypes.Email, DevUserEmail),
            new Claim("email", DevUserEmail),
            new Claim(ClaimTypes.GivenName, "Dev"),
            new Claim("given_name", "Dev"),
            new Claim(ClaimTypes.Surname, "User"),
            new Claim("family_name", "User"),
            new Claim("name", "Dev User"),
            new Claim(ClaimTypes.Name, "Dev User"),
        };

        var identity = new ClaimsIdentity(claims, SchemeName);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, SchemeName);

        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}