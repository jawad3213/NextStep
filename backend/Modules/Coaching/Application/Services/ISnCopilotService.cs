using NextStep.Modules.Chatbot.DTOs;

namespace NextStep.Modules.Chatbot.Interfaces;

public interface ISnCopilotService
{
    Task<SnChatAgentResponse> ChatAsync(
        Guid userId,
        string userName,
        SnUserChatRequest request,
        CancellationToken cancellationToken = default);

    Task<List<SnStarterSuggestionItem>> GetStarterSuggestionsAsync();
}
