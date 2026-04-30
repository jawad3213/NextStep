using NextStep.Shared.Http;
using NextStep.Modules.Candidature.Repositories;
using NextStep.Modules.Email.DTOs;
using NextStep.Modules.Email.Models;
using NextStep.Modules.Email.Repositories;

namespace NextStep.Modules.Email.Services;

public class EmailService : IEmailService
{
    private readonly ICandidatureRepository _candidatureRepository;
    private readonly IEmailDraftRepository _emailDraftRepository;
    private readonly IAgentHttpClient _agentHttpClient;
    private readonly ILogger<EmailService> _logger;

    public EmailService(
        ICandidatureRepository candidatureRepository,
        IEmailDraftRepository emailDraftRepository,
        IAgentHttpClient agentHttpClient,
        ILogger<EmailService> logger)
    {
        _candidatureRepository = candidatureRepository;
        _emailDraftRepository = emailDraftRepository;
        _agentHttpClient = agentHttpClient;
        _logger = logger;
    }

    public async Task<EmailDraftDto> GenerateDraftAsync(
        GenerateEmailDraftDto dto,
        CancellationToken cancellationToken = default)
    {
        var candidature = await _candidatureRepository.GetByIdAsync(
            dto.CandidatureId,
            cancellationToken);

        if (candidature is null)
            throw new KeyNotFoundException("Candidature not found.");

        PythonEmailResponse pythonResponse;

        try
        {
            var pythonRequest = new
            {
                candidature_id = candidature.IdCandidature,
                user_id = candidature.IdUtilisateur,
                offer_id = candidature.IdOffre,
                email_type = dto.EmailType,
                language = dto.Language,
                tone = dto.Tone
            };

            pythonResponse = await _agentHttpClient.PostAsync<object, PythonEmailResponse>(
                "/generate-email",
                pythonRequest,
                cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Email agent unavailable. Using temporary placeholder draft.");

            pythonResponse = new PythonEmailResponse
            {
                Subject = "Candidature pour l'offre sélectionnée",
                Body = """
                Bonjour,

                Je vous adresse ma candidature pour l'offre sélectionnée.

                Mon profil correspond aux besoins du poste et je serais ravi d'échanger avec vous à ce sujet.

                Cordialement,
                """,
                RecipientEmail = null,
                DetectedLanguage = dto.Language
            };
        }

        var draft = new EmailDraft
        {
            CandidatureId = candidature.IdCandidature,
            EmailType = dto.EmailType,
            RecipientEmail = pythonResponse.RecipientEmail,
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

        return MapToDto(draft);
    }

    public async Task<List<EmailDraftDto>> GetDraftsByCandidatureAsync(
        Guid candidatureId,
        CancellationToken cancellationToken = default)
    {
        var drafts = await _emailDraftRepository.GetByCandidatureIdAsync(
            candidatureId,
            cancellationToken);

        return drafts.Select(MapToDto).ToList();
    }

    private static EmailDraftDto MapToDto(EmailDraft draft)
    {
        return new EmailDraftDto
        {
            Id = draft.Id,
            CandidatureId = draft.CandidatureId,
            EmailType = draft.EmailType,
            RecipientEmail = draft.RecipientEmail,
            Subject = draft.Subject,
            Body = draft.Body,
            Language = draft.Language,
            IsApproved = draft.IsApproved,
            IsSent = draft.IsSent,
            CreatedAtUtc = draft.CreatedAtUtc,
            UpdatedAtUtc = draft.UpdatedAtUtc,
            SentAtUtc = draft.SentAtUtc,
            ErrorMessage = draft.ErrorMessage
        };
    }

    private sealed class PythonEmailResponse
    {
        public string Subject { get; set; } = string.Empty;
        public string Body { get; set; } = string.Empty;
        public string? RecipientEmail { get; set; }
        public string? DetectedLanguage { get; set; }
    }
}