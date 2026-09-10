using System.Text.Json.Serialization;

namespace NextStep.Modules.Chatbot.DTOs;

public record SnChatMessageDto(
    [property: JsonPropertyName("role")] string Role,
    [property: JsonPropertyName("content")] string Content
);

public record SnChatCardDto(
    [property: JsonPropertyName("id")] string? Id,
    [property: JsonPropertyName("id_offre")] string? IdOffre,
    [property: JsonPropertyName("entreprise")] string Entreprise,
    [property: JsonPropertyName("role")] string Role,
    [property: JsonPropertyName("statut")] string Statut,
    [property: JsonPropertyName("channel")] string Channel,
    [property: JsonPropertyName("channel_url")] string? ChannelUrl,
    [property: JsonPropertyName("application_date")] string? ApplicationDate,
    [property: JsonPropertyName("has_response")] bool HasResponse,
    [property: JsonPropertyName("response_status")] string? ResponseStatus,
    [property: JsonPropertyName("notes")] string? Notes,
    [property: JsonPropertyName("response_summary")] string? ResponseSummary,
    [property: JsonPropertyName("recommended_action")] string? RecommendedAction,
    [property: JsonPropertyName("follow_up_needed")] bool FollowUpNeeded
);

public record SnChatAgentRequest(
    [property: JsonPropertyName("user_id")] string UserId,
    [property: JsonPropertyName("message")] string Message,
    [property: JsonPropertyName("user_name")] string? UserName,
    [property: JsonPropertyName("history")] List<SnChatMessageDto>? History
);

public record SnChatAgentResponse(
    [property: JsonPropertyName("markdown_text")] string MarkdownText,
    [property: JsonPropertyName("actions_performed")] List<string> ActionsPerformed,
    [property: JsonPropertyName("cards")] List<SnChatCardDto> Cards,
    [property: JsonPropertyName("follow_up_suggestions")] List<string> FollowUpSuggestions
);

public record SnUserChatRequest(
    string Message,
    List<SnChatMessageDto>? History
);

public record SnStarterSuggestionItem(
    string Title,
    string Description,
    string Prompt,
    string Category,
    string Icon
);
