using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace backend.Modules.Profile.Models
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
    }
}
