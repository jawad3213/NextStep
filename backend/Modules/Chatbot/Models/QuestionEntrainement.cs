using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace NextStep.Modules.Chatbot.Models;

[Table("question_entrainement")]
public class QuestionEntrainement
{
    [Key]
    [Column("id_question")]
    public Guid IdQuestion { get; set; } = Guid.NewGuid();

    [Column("id_session")]
    public Guid IdSession { get; set; }

    [Column("texte_question")]
    public string TexteQuestion { get; set; } = string.Empty;

    // "behavioral" | "technical" | "situational"
    [Column("type_question")]
    [MaxLength(20)]
    public string TypeQuestion { get; set; } = "behavioral";

    // "generated" | "glassdoor"
    [Column("source")]
    [MaxLength(20)]
    public string Source { get; set; } = "generated";

    [Column("company_specific")]
    public bool CompanySpecific { get; set; } = false;

    [Column("conseil_reponse")]
    public string? ConseilReponse { get; set; }

    [Column("reponse_utilisateur")]
    public string? ReponseUtilisateur { get; set; }

    [Column("correction_ia")]
    public string? CorrectionIa { get; set; }

    [Column("score_reponse")]
    public int? ScoreReponse { get; set; }

    [Column("ordre")]
    public int Ordre { get; set; } = 0;

    // Navigation
    [ForeignKey("IdSession")]
    public SessionCoaching? Session { get; set; }
}