using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace NextStep.Modules.Profile.Models
{
    [Table("projet")]
    public class Projet
    {
        [Key]
        [Column("id_projet")]
        public Guid Id { get; set; } = Guid.NewGuid();

        [Column("id_utilisateur")]
        public Guid UserId { get; set; }

        [Column("titre_projet")]
        public string? TitreProjet { get; set; }

        [Column("description")]
        public string? Description { get; set; }

        [Column("technologies_utilisees")]
        public string? TechnologiesUtilisees { get; set; }

        [Column("lien_projet")]
        public string? LienProjet { get; set; }

        [Column("date_realisation")]
        public DateTime? DateRealisation { get; set; }

        [Column("demo_url")]
        public string? DemoUrl { get; set; }

        [Column("image_url")]
        public string? ImageUrl { get; set; }

        [Column("is_university")]
        public bool IsUniversity { get; set; }

        [Column("is_valid")]
        public bool IsValid { get; set; } = false;
    }
}   
