using NextStep.Modules.Coaching.Application.Dtos;

namespace NextStep.Modules.Coaching.Application.Services;

/// <summary>
/// HTTP contract to Python FastAPI.
/// Replacing the implementation here is enough to migrate to a microservice (gRPC, message broker...).
/// The rest of the code (Service, Controller) does not change.
/// </summary>
public interface IAgentHttpClient
{
    /// <summary>POST /api/chatbot/questions</summary>
    Task<QuestionsResponse> PostQuestionsAsync(QuestionsRequest request);

    /// <summary>POST /api/chatbot/free-chat</summary>
    Task<FreeChatResponse> PostFreeChatAsync(FreeChatRequest request);

    /// <summary>POST /api/chatbot/interview/start</summary>
    Task<StartSessionResponse> PostStartInterviewAsync(StartSessionRequest request);

    /// <summary>POST /api/chatbot/interview/message</summary>
    Task<SendMessageResponse> PostSendMessageAsync(SendMessageRequest request);

    /// <summary>POST /api/chatbot/interview/end</summary>
    Task<EndSessionResponse> PostEndInterviewAsync(EndSessionRequest request);

    /// <summary>POST /api/chatbot/salary</summary>
    Task<SalaryResponse> PostSalaryAsync(SalaryRequest request);

    /// <summary>POST /api/agents/sn/chat</summary>
    Task<SnChatAgentResponse> PostSnChatAsync(SnChatAgentRequest request);
}