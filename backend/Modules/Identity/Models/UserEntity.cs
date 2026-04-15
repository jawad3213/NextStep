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

        [Column("lien_linkedin")]
        public string? LienLinkedin { get; set; }

        [Column("lien_github")]
        public string? LienGithub { get; set; }

        [Column("lien_portfolio")]
        public string? LienPortfolio { get; set; }

        [Column("resume_professionnel")]
        public string? ResumeProfessionnel { get; set; }

        [Column("coordonnees")]
        public string? Coordonnees { get; set; }

        [Column("objectif")]
        public string? Objectif { get; set; }

        [Column("niveau")]
        public string? Niveau { get; set; }

        [Column("secteur")]
        public string? Secteur { get; set; }

        [Column("onboarding_completed")]
        public bool OnboardingCompleted { get; set; } = false;

        [Column("onboarding_step")]
        public int OnboardingStep { get; set; } = 0;

        [Column("onboarding_data", TypeName = "jsonb")]
        public string? OnboardingData { get; set; }

        [Column("profile_score")]
        public int ProfileScore { get; set; } = 0;

        [Column("date_inscription")]
        public DateTime DateInscription { get; set; } = DateTime.UtcNow;
    }
}
