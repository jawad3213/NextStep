using NextStep.Modules.Coaching.Infrastructure.Agents;
using Microsoft.Extensions.Logging;
using NextStep.Modules.Coaching.Application.Dtos;
using NextStep.Modules.Coaching.Application.Services;
using NextStep.Shared.ErrorHandling;

namespace NextStep.Modules.Coaching.Application.Services;

public class SnCopilotService : ISnCopilotService
{
    private readonly IAgentHttpClient _agentClient;
    private readonly ILogger<SnCopilotService> _logger;

    public SnCopilotService(IAgentHttpClient agentClient, ILogger<SnCopilotService> logger)
    {
        _agentClient = agentClient;
        _logger = logger;
    }

    public async Task<SnChatAgentResponse> ChatAsync(
        Guid userId,
        string userName,
        SnUserChatRequest request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Message))
            throw new BadRequestException("Message cannot be empty.");

        _logger.LogInformation("SN Copilot chat initiated for user {UserId} ({UserName})", userId, userName);

        var agentRequest = new SnChatAgentRequest(
            UserId: userId.ToString(),
            Message: request.Message,
            UserName: userName,
            History: request.History
        );

        try
        {
            return await _agentClient.PostSnChatAsync(agentRequest);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error interacting with SN Copilot.");
            throw new OperationFailedException("Failed to communicate with SN Copilot.", ex);
        }
    }

    public Task<List<SnStarterSuggestionItem>> GetStarterSuggestionsAsync()
    {
        var starters = new List<SnStarterSuggestionItem>
        {
            new(
                Title: "My Profile",
                Description: "Comprehensive summary of your skills, education, and experiences",
                Prompt: "Summarize my full profile with my skills and experiences",
                Category: "Profile",
                Icon: "user"
            ),
            new(
                Title: "CV Matching",
                Description: "Analyze compatibility between your profile and active applications",
                Prompt: "Match my profile with my active applications",
                Category: "Intelligence",
                Icon: "target"
            ),
            new(
                Title: "Portfolio Review",
                Description: "View your last 10 applications and recent status updates",
                Prompt: "Show my last 10 applications with their statuses and dates",
                Category: "Portfolio",
                Icon: "layers"
            ),
            new(
                Title: "Follow-up Priorities",
                Description: "Identify stagnant applications requiring follow-up",
                Prompt: "Which pending applications should I follow up on first?",
                Category: "Strategy",
                Icon: "clock"
            ),
            new(
                Title: "Metrics & Velocity",
                Description: "Executive summary of response rates and pipeline conversion",
                Prompt: "Analyze my conversion rates and pipeline statistics",
                Category: "Analytics",
                Icon: "bar-chart-2"
            ),
            new(
                Title: "Quick Update",
                Description: "Update an application status or record an interview",
                Prompt: "Move my latest application to 'Interview' status",
                Category: "Action",
                Icon: "check-circle"
            )
        };

        return Task.FromResult(starters);
    }
}
