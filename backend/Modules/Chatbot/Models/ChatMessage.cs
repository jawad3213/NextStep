using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace NextStep.Modules.Chatbot.Models;

[Table("chat_message")]
public class ChatMessage
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; } = Guid.NewGuid();

    // Identifie un fil de conversation (tab Questions chat libre)
    [Column("thread_id")]
    public Guid ThreadId { get; set; }

    [Column("id_utilisateur")]
    public Guid IdUtilisateur { get; set; }

    [Column("id_session")]
    public Guid? IdSession { get; set; }

    [Column("id_candidature")]
    public Guid? IdCandidature { get; set; }

    // "questions" | "interview" | "salary"
    [Column("chat_type")]
    [MaxLength(20)]
    public string ChatType { get; set; } = string.Empty;

    // "user" | "ai"
    [Column("sender")]
    [MaxLength(10)]
    public string Sender { get; set; } = string.Empty;

    [Column("content")]
    public string Content { get; set; } = string.Empty;

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    [ForeignKey("IdSession")]
    public SessionCoaching? Session { get; set; }
}