using NextStep.Modules.Identity.Models;

namespace NextStep.Modules.Identity.DTOs
{
    public class UserSyncDto
    {
        public string KeycloakId { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string? FirstName { get; set; }
        public string? LastName { get; set; }
    }

    public class SoftOnboardingDto
    {
        public ObjectifEnum Objectif { get; set; }
        public NiveauEnum Niveau { get; set; }
        public SecteurEnum Secteur { get; set; }
    }

    public class ProfileStatusDto
    {
        public bool IsComplete { get; set; }
        public bool OnboardingCompleted { get; set; }
        public int ProfileScore { get; set; }
        public List<string> MissingSections { get; set; } = new();
    }
}
