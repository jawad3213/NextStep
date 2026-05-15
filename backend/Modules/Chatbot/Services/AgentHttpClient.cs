using System.Net.Http.Json;
using System.Text.Json;
using NextStep.Modules.Chatbot.DTOs;
using NextStep.Modules.Chatbot.Interfaces;

namespace NextStep.Modules.Chatbot.Services;

/// <summary>
/// Proxy HTTP pur vers Python FastAPI (LangGraph agents).
/// Aucune logique métier ici — uniquement sérialisation/désérialisation JSON.
/// Pour migrer en microservice : remplacer cette classe par un client gRPC ou message broker.
/// </summary>
public class AgentHttpClient : IAgentHttpClient
{
    private readonly HttpClient _http;
    private readonly JsonSerializerOptions _jsonOptions;

    // Injecté depuis appsettings.json → AgentsService:BaseUrl
    public AgentHttpClient(HttpClient http)
    {
        _http = http;
        _jsonOptions = new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
        };
    }

    public async Task<QuestionsResponse> PostQuestionsAsync(QuestionsRequest request)
    {
        var response = await _http.PostAsJsonAsync("/api/chatbot/questions", request, _jsonOptions);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<QuestionsResponse>(_jsonOptions)
               ?? throw new InvalidOperationException("Empty response from Python agent (questions)");
    }

    public async Task<FreeChatResponse> PostFreeChatAsync(FreeChatRequest request)
    {
        var response = await _http.PostAsJsonAsync("/api/chatbot/free-chat", request, _jsonOptions);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<FreeChatResponse>(_jsonOptions)
               ?? throw new InvalidOperationException("Empty response from Python agent (free-chat)");
    }

    public async Task<StartSessionResponse> PostStartInterviewAsync(StartSessionRequest request)
    {
        var response = await _http.PostAsJsonAsync("/api/chatbot/interview/start", request, _jsonOptions);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<StartSessionResponse>(_jsonOptions)
               ?? throw new InvalidOperationException("Empty response from Python agent (interview/start)");
    }

    public async Task<SendMessageResponse> PostSendMessageAsync(SendMessageRequest request)
    {
        var response = await _http.PostAsJsonAsync("/api/chatbot/interview/message", request, _jsonOptions);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<SendMessageResponse>(_jsonOptions)
               ?? throw new InvalidOperationException("Empty response from Python agent (interview/message)");
    }

    public async Task<EndSessionResponse> PostEndInterviewAsync(EndSessionRequest request)
    {
        var response = await _http.PostAsJsonAsync("/api/chatbot/interview/end", request, _jsonOptions);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<EndSessionResponse>(_jsonOptions)
               ?? throw new InvalidOperationException("Empty response from Python agent (interview/end)");
    }

    public async Task<SalaryResponse> PostSalaryAsync(SalaryRequest request)
    {
        var response = await _http.PostAsJsonAsync("/api/chatbot/salary", request, _jsonOptions);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<SalaryResponse>(_jsonOptions)
               ?? throw new InvalidOperationException("Empty response from Python agent (salary)");
    }
}