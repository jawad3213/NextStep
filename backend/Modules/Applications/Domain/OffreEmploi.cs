// ============================================================
// Modules/Offer/Models/OffreEmploi.cs
// Entité EF Core — table offres_emploi
// ============================================================
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace NextStep.Modules.Offer.Models;

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

    /// <summary>JSON structuré retourné par Agent 1 (LLM), stocké en JSONB PostgreSQL.</summary>
    [Column("analyse_json", TypeName = "jsonb")]
    public string? AnalyseJson { get; set; }

    [Column("date_creation")]
    public DateTime DateCreation { get; set; } = DateTime.UtcNow;
}
