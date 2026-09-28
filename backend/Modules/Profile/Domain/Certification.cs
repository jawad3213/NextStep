using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace NextStep.Modules.Profile.Models
{
    [Table("certification")]
    public class Certification
    {
        [Key]
        [Column("id_certification")]
        public Guid Id { get; set; } = Guid.NewGuid();

        [Column("id_utilisateur")]
        public Guid UserId { get; set; }

        [Column("titre")]
        public string? Titre { get; set; }

        [Column("organisation")]
        public string? Organisation { get; set; }

        [Column("date_obtention")]
        public DateTime? DateObtention { get; set; }

        [Column("id_credential")]
        public string? IdCredential { get; set; }

        [Column("url_credential")]
        public string? UrlCredential { get; set; }
    }
}
