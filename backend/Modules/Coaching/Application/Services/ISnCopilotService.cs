using NextStep.Modules.Coaching.Application.Dtos;

namespace NextStep.Modules.Coaching.Application.Services;

public interface ISnCopilotService
{
    Task<SnChatAgentResponse> ChatAsync(
        Guid userId,
        string userName,
        SnUserChatRequest request,
        CancellationToken cancellationToken = default);

    Task<List<SnStarterSuggestionItem>> GetStarterSuggestionsAsync();
}
