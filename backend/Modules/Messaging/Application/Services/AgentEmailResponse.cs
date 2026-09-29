using NextStep.Modules.Messaging.Infrastructure.Gmail;
namespace NextStep.Modules.Messaging.Application.Services;

/// <summary>Email returned by the Python email agent (subject/body/language/tone).</summary>
public sealed class AgentEmailResponse
{
    public string Subject  { get; set; } = string.Empty;
    public string Body     { get; set; } = string.Empty;
    public string Language { get; set; } = string.Empty;
    public string Tone     { get; set; } = string.Empty;
}
