namespace NextStep.Modules.Candidature.Models;

public enum CandidatureStatut
{
    BROUILLON = 0,
    ENVOYE = 1,
    ACCUSE_RECEPTION = 2,
    EN_COURS_EXAMEN = 3,
    RELANCE_NECESSAIRE = 4,
    RELANCE_ENVOYEE = 5,
    REPONSE_RECUE = 6,
    TEST_TECHNIQUE = 7,
    ENTRETIEN_PROPOSE = 8,
    ENTRETIEN_EFFECTUE = 9,
    OFFRE_RECUE = 10,
    ACCEPTE = 11,
    REFUSE = 12,
    ABANDONNE = 13
}

public static class CandidatureStatutExtensions
{
    public static string ToDisplayString(this CandidatureStatut statut) => statut switch
    {
        CandidatureStatut.BROUILLON => "Brouillon",
        CandidatureStatut.ENVOYE => "Envoyé",
        CandidatureStatut.ACCUSE_RECEPTION => "Accusé de réception",
        CandidatureStatut.EN_COURS_EXAMEN => "En cours d'examen",
        CandidatureStatut.RELANCE_NECESSAIRE => "Relance nécessaire",
        CandidatureStatut.RELANCE_ENVOYEE => "Relance envoyée",
        CandidatureStatut.REPONSE_RECUE => "Réponse reçue",
        CandidatureStatut.TEST_TECHNIQUE => "Test technique",
        CandidatureStatut.ENTRETIEN_PROPOSE => "Entretien proposé",
        CandidatureStatut.ENTRETIEN_EFFECTUE => "Entretien effectué",
        CandidatureStatut.OFFRE_RECUE => "Offre reçue",
        CandidatureStatut.ACCEPTE => "Accepté",
        CandidatureStatut.REFUSE => "Refusé",
        CandidatureStatut.ABANDONNE => "Abandonné",
        _ => statut.ToString()
    };

    public static CandidatureStatut ParseFromString(string value)
    {
        if (Enum.TryParse<CandidatureStatut>(value, true, out var result))
            return result;

        return value.ToUpperInvariant() switch
        {
            "EN_ATTENTE" => CandidatureStatut.EN_COURS_EXAMEN,
            "VU" => CandidatureStatut.ACCUSE_RECEPTION,
            "SENT" => CandidatureStatut.ENVOYE,
            _ => CandidatureStatut.ENVOYE
        };
    }
}
