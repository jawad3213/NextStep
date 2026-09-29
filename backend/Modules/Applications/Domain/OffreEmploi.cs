// ============================================================
// Modules/Applications/Models/OffreEmploi.cs
// EF Core entity — offres_emploi table
// ============================================================
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace NextStep.Modules.Applications.Domain;

[Table("offres_emploi")]
public class OffreEmploi
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    [Column("utilisateur_id")]
    public Guid UtilisateurId { get; set; }

    [Required]
    [Column("texte_brut")]
    public string TexteBrut { get; set; } = string.Empty;

    /// <summary>Structured JSON returned by Agent 1 (LLM), stored as PostgreSQL JSONB.</summary>
    [Column("analyse_json", TypeName = "jsonb")]
    public string? AnalyseJson { get; set; }

    [Column("date_creation")]
    public DateTime DateCreation { get; set; } = DateTime.UtcNow;
}
