using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace NextStep.Modules.Chatbot.Models;

[Table("session_coaching")]
public class SessionCoaching
{
    [Key]
    [Column("id_session")]
    public Guid IdSession { get; set; } = Guid.NewGuid();

    [Column("id_candidature")]
    public Guid? IdCandidature { get; set; }

    [Column("id_utilisateur")]
    public Guid IdUtilisateur { get; set; }

    // "offer" | "arena"
    [Column("mode")]
    [MaxLength(10)]
    public string Mode { get; set; } = "arena";

    [Column("language")]
    [MaxLength(10)]
    public string Language { get; set; } = "en";

    [Column("duration_minutes")]
    public int DurationMinutes { get; set; } = 20;

    // "pending" | "active" | "completed"
    [Column("status")]
    [MaxLength(20)]
    public string Status { get; set; } = "pending";

    // Arena mode only
    [Column("domain")]
    [MaxLength(100)]
    public string? Domain { get; set; }

    [Column("level")]
    [MaxLength(20)]
    public string? Level { get; set; }

    // JSONB — stocké comme string, ex: ["Python","SQL"]
    [Column("focus_areas")]
    public string? FocusAreas { get; set; }

    // Résultats post-session
    [Column("score_entretien")]
    public int? ScoreEntretien { get; set; }

    // JSONB — score global + 5 dimensions + coaching tips
    [Column("feedback_json")]
    public string? FeedbackJson { get; set; }

    [Column("date_session")]
    public DateTime DateSession { get; set; } = DateTime.UtcNow;

    [Column("completed_at")]
    public DateTime? CompletedAt { get; set; }
}