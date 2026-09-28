using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Collections.Generic;

namespace NextStep.Modules.Profile.Models
{
    [Table("experience")]
    public class Experience
    {
        [Key]
        [Column("id_experience")]
        public Guid Id { get; set; } = Guid.NewGuid();

        [Column("id_utilisateur")]
        public Guid UserId { get; set; }

        [Column("entreprise")]
        public string? Entreprise { get; set; }

        [Column("poste")]
        public string? Poste { get; set; }

        [Column("date_debut")]
        public DateTime? DateDebut { get; set; }

        [Column("date_fin")]
        public DateTime? DateFin { get; set; }

        [Column("missions")]
        public string? Missions { get; set; }

        [Column("ville")]
        public string? Ville { get; set; }

        [Column("type_contrat")]
        public string? TypeContrat { get; set; }

        [Column("taches", TypeName = "jsonb")]
        public List<string> Taches { get; set; } = new();

        [Column("is_valid")]
        public bool IsValid { get; set; } = false;
    }
}
