using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace NextStep.Modules.Profile.Models
{
    [Table("formation")]
    public class Formation
    {
        [Key]
        [Column("id_formation")]
        public Guid Id { get; set; } = Guid.NewGuid();

        [Column("id_utilisateur")]
        public Guid UserId { get; set; }

        [Column("etablissement")]
        public string? Etablissement { get; set; }

        [Column("diplome")]
        public string? Diplome { get; set; }

        [Column("annee")]
        public int Annee { get; set; }

        [Column("ville")]
        public string? Ville { get; set; }

        [Column("specialisation")]
        public string? Specialisation { get; set; }

        [Column("mention")]
        public string? Mention { get; set; }

        [Column("annee_fin")]
        public int? AnneeFin { get; set; }
    }
}
