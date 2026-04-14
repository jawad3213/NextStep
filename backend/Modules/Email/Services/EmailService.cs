using backend.Infrastructure.Http;
using backend.Modules.Candidature.Repositories;
using backend.Modules.Email.DTOs;
using backend.Modules.Email.Models;
using backend.Modules.Email.Repositories;

namespace backend.Modules.Email.Services;

public class EmailService : IEmailService
{
    private readonly ICandidatureRepository _candidatureRepository;
    private readonly IEmailDraftRepository _emailDraftRepository;
    private readonly IAgentHttpClient _agentHttpClient;

    public EmailService(
        ICandidatureRepository candidatureRepository,
        IEmailDraftRepository emailDraftRepository,
        IAgentHttpClient agentHttpClient)
    {
        _candidatureRepository = candidatureRepository;
        _emailDraftRepository = emailDraftRepository;
        _agentHttpClient = agentHttpClient;
    }

    public async Task<EmailDraftDto> GenerateDraftAsync(
        GenerateEmailDraftDto dto,
        CancellationToken cancellationToken = default)
    {
        var candidature = await _candidatureRepository.GetByIdAsync(dto.CandidatureId, cancellationToken);

        if (candidature is null)
        {
            throw new KeyNotFoundException("Candidature not found.");
        }

        var pythonRequest = new
        {
            email_type = dto.EmailType,
            language = dto.Language,
            tone = dto.Tone,
            candidate = new
            {
                full_name = dto.CandidateFullName,
                title = dto.CandidateTitle,
                skills = dto.Skills,
                highlights = dto.Highlights
            },
            job_offer = new
            {
                company_name = candidature.CompanyName,
                job_title = candidature.JobTitle,
                summary = candidature.JobOfferText
            }
        };

        var pythonResponse = await _agentHttpClient.PostAsync<object, PythonEmailResponse>(
            "/generate-email",
            pythonRequest,
            cancellationToken);

        var draft = new EmailDraft
        {
            CandidatureId = candidature.Id,
            EmailType = dto.EmailType,
            Subject = pythonResponse.Subject,
            Body = pythonResponse.Body,
            Language = string.IsNullOrWhiteSpace(pythonResponse.DetectedLanguage)
                ? dto.Language
                : pythonResponse.DetectedLanguage,
            IsApproved = false,
            IsSent = false,
            CreatedAtUtc = DateTime.UtcNow
        };

        await _emailDraftRepository.AddAsync(draft, cancellationToken);

        return new EmailDraftDto
        {
            Id = draft.Id,
            CandidatureId = draft.CandidatureId,
            EmailType = draft.EmailType,
            Subject = draft.Subject,
            Body = draft.Body,
            Language = draft.Language,
            IsApproved = draft.IsApproved,
            IsSent = draft.IsSent,
            CreatedAtUtc = draft.CreatedAtUtc
        };
    }

    private sealed class PythonEmailResponse
    {
        public string Subject { get; set; } = string.Empty;
        public string Body { get; set; } = string.Empty;
        public string? DetectedLanguage { get; set; }
    }
}