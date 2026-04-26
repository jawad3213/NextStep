namespace NextStep.Infrastructure.Http;

public class AgentPythonOptions
{
    public string BaseUrl { get; set; } = "http://localhost:8000";

    public string? Url { get; set; }

    public string ResolveBaseUrl()
    {
        if (!string.IsNullOrWhiteSpace(BaseUrl))
            return BaseUrl;

        if (!string.IsNullOrWhiteSpace(Url))
            return Url;

        return "http://localhost:8000";
    }
}