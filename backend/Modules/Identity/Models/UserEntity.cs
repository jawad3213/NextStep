using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace backend.Modules.Identity.Models
{
    [Table("utilisateur")]
    public class UserEntity
    {
        [Key]
        [Column("id_utilisateur")]
        public Guid Id { get; set; } = Guid.NewGuid();

        [Required]
        [Column("keycloak_id")]
        public string KeycloakId { get; set; } = string.Empty;

        [Column("nom")]
        public string? Nom { get; set; }

        [Column("prenom")]
        public string? Prenom { get; set; }

        [Required]
        [Column("email")]
        public string Email { get; set; } = string.Empty;

        [Column("date_inscription")]
        public DateTime DateInscription { get; set; } = DateTime.UtcNow;

        // Autres champs (Linkedin, Github...) peuvent être ajoutés ici selon votre init.sql
    }
}
