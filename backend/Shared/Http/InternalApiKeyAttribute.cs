using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace NextStep.Shared.Http;

/// <summary>
/// Protects the internal endpoints used by the Python agents: the caller must send the
/// shared secret AGENTS_API_KEY in <c>X-Internal-Api-Key</c> (the same secret the backend
/// sends to the agents). No user token is involved; the endpoints take the user id as input.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public sealed class InternalApiKeyAttribute : Attribute, IAuthorizationFilter
{
    public void OnAuthorization(AuthorizationFilterContext context)
    {
        var configuration = context.HttpContext.RequestServices.GetRequiredService<IConfiguration>();
        var expected = configuration["AGENTS_API_KEY"];
        var provided = context.HttpContext.Request.Headers[AgentApiKeyHandler.HeaderName].ToString();

        if (string.IsNullOrEmpty(expected) || !CryptographicOperations.FixedTimeEquals(
                Encoding.UTF8.GetBytes(expected), Encoding.UTF8.GetBytes(provided)))
        {
            context.Result = new UnauthorizedObjectResult(new { error = "Unauthorized", type = "InternalAuthError" });
        }
    }
}
