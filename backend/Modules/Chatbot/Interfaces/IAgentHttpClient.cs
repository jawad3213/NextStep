using NextStep.Modules.Chatbot.DTOs;

namespace NextStep.Modules.Chatbot.Interfaces;

/// <summary>
/// Contrat HTTP vers Python FastAPI.
/// Remplacer l'implémentation ici suffit pour migrer en microservice (gRPC, message broker...).
/// Le reste du code (Service, Controller) ne change pas.
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
}