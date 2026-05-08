using NextStep.Modules.Email.Models;
using NextStep.Modules.Offer.Models;

namespace NextStep.Modules.Candidature.Models;

public class Candidature
{
    public Guid IdCandidature { get; set; } = Guid.NewGuid();

    public Guid IdUtilisateur { get; set; }

    public Guid IdOffre { get; set; }

    public OffreEmploi? Offre { get; set; }

    public DateTime DateCreation { get; set; } = DateTime.UtcNow;

    public bool InclureLettreMotivation { get; set; } = false;

    public string Statut { get; set; } = "EN_ATTENTE";

    // ── Email reply tracking ──────────────────────────────────────────────────────

    /// <summary>Tracks whether a reply to the sent email has been detected. Default: "EN_ATTENTE".</summary>
    public string ResponseStatus { get; set; } = "EN_ATTENTE";

    /// <summary>True when a reply from the recruiter/recipient has been detected.</summary>
    public bool HasResponse { get; set; } = false;

    /// <summary>UTC timestamp of the last time the Gmail thread was polled for replies.</summary>
    public DateTime? LastCheckedAtUtc { get; set; }

    /// <summary>UTC timestamp of the detected reply message. Null until a reply is found.</summary>
    public DateTime? LastResponseAtUtc { get; set; }

    // ── AI Classification fields ──────────────────────────────────────────────────

    /// <summary>From address of the detected reply message. Populated by CheckEmailRepliesJob.</summary>
    public string? LastResponseFrom { get; set; }

    /// <summary>Short snippet from the detected reply. Populated by CheckEmailRepliesJob.</summary>
    public string? LastResponseSnippet { get; set; }

    /// <summary>LLM-generated summary of the recruiter reply (in French).</summary>
    public string? ResponseSummary { get; set; }

    /// <summary>LLM-generated recommended next action for the candidate (in French).</summary>
    public string? RecommendedAction { get; set; }

    /// <summary>Classifier confidence score, normalised to [0, 1]. Null until classified.</summary>
    public double? ResponseConfidence { get; set; }

    /// <summary>UTC timestamp when the LLM classification was last run. Null until classified.</summary>
    public DateTime? ResponseClassifiedAtUtc { get; set; }

    public ICollection<EmailDraft> EmailDrafts { get; set; } = new List<EmailDraft>();
}