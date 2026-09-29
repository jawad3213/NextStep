using NextStep.Modules.Profile.Domain;

namespace NextStep.Modules.Profile.Application.Dtos {
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
        public bool ProfileCompleted { get; set; }
        public int ProfileScore { get; set; }
        public int CompletionPercent { get; set; }
        public List<string> MissingSections { get; set; } = new();
    }

    public class CompleteProfileResult
    {
        public bool Succeeded { get; set; }
        public string Message { get; set; } = string.Empty;
        public int CompletionPercent { get; set; }
        public int RequiredPercent { get; set; }
    }
}
