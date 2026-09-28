using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace NextStep.Modules.Profile.Models
{
    [Table("competence")]
    public class Competence
    {
        [Key]
        [Column("id_competence")]
        public Guid Id { get; set; } = Guid.NewGuid();

        [Column("id_utilisateur")]
        public Guid UserId { get; set; }

        [Column("nom")]
        public string? Nom { get; set; }

        [Column("niveau")]
        public int Niveau { get; set; }

        [Column("type_competence")]
        public string? TypeCompetence { get; set; }

        [Column("is_valid")]
        public bool IsValid { get; set; } = false;
    }
}
