namespace NextStep.Shared.Http;

/// <summary>
/// Adds the shared secret (AGENTS_API_KEY) to every request sent to the Python agents
/// service. The agents reject any request without it, so only this backend can call them.
/// </summary>
public class AgentApiKeyHandler(IConfiguration configuration) : DelegatingHandler
{
    public const string HeaderName = "X-Internal-Api-Key";

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var key = configuration["AGENTS_API_KEY"];
        if (!string.IsNullOrEmpty(key))
        {
            request.Headers.Remove(HeaderName);
            request.Headers.TryAddWithoutValidation(HeaderName, key);
        }
        return base.SendAsync(request, cancellationToken);
    }
}
