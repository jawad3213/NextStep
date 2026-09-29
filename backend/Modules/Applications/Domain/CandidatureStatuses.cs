namespace NextStep.Modules.Applications.Domain;

/// <summary>Application statuses known to the board (see candidature-status.ts in the frontend).</summary>
public static class CandidatureStatuses
{
    public static readonly IReadOnlySet<string> All = new HashSet<string>(StringComparer.Ordinal)
    {
        "BROUILLON", "ENVOYE", "ACCUSE_RECEPTION", "EN_ATTENTE", "EN_COURS_EXAMEN",
        "RELANCE_NECESSAIRE", "RELANCE_ENVOYEE", "REPONSE_RECUE", "TEST_TECHNIQUE",
        "ENTRETIEN_PROPOSE", "ENTRETIEN_EFFECTUE", "OFFRE_RECUE", "ACCEPTE", "REFUSE", "ABANDONNE",
    };
}
