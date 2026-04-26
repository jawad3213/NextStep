namespace NextStep.Modules.Profile.DTOs
{
    public class ExperienceDto
    {
        public Guid? Id { get; set; }
        public string? Entreprise { get; set; }
        public string? Poste { get; set; }
        public DateTime? DateDebut { get; set; }
        public DateTime? DateFin { get; set; }
        public string? Missions { get; set; }
    }

    public class FormationDto
    {
        public Guid? Id { get; set; }
        public string? Etablissement { get; set; }
        public string? Diplome { get; set; }
        public int Annee { get; set; }
    }

    public class ProjetDto
    {
        public Guid? Id { get; set; }
        public string? TitreProjet { get; set; }
        public string? Description { get; set; }
        public string? TechnologiesUtilisees { get; set; }
        public string? LienProjet { get; set; }
        public DateTime? DateRealisation { get; set; }
    }

    public class CompetenceDto
    {
        public Guid? Id { get; set; }
        public string? Nom { get; set; }
        public int Niveau { get; set; }
        public string? TypeCompetence { get; set; }
    }

    public class PersonalInfoDto
    {
        public string? Nom { get; set; }
        public string? Prenom { get; set; }
        public string? Email { get; set; }
        public string? Coordonnees { get; set; }
        public string? LienLinkedin { get; set; }
        public string? LienGithub { get; set; }
        public string? LienPortfolio { get; set; }
        public string? ResumeProfessionnel { get; set; }
    }

    public class OnboardingDto
    {
        public string Objectif { get; set; } = string.Empty;
        public string Niveau { get; set; } = string.Empty;
        public string Secteur { get; set; } = string.Empty;
    }

    public class FullProfileDto
    {
        public PersonalInfoDto PersonalInfo { get; set; } = new();
        public string? Objectif { get; set; }
        public string? Niveau { get; set; }
        public string? Secteur { get; set; }
        public bool OnboardingCompleted { get; set; }
        public List<ExperienceDto> Experiences { get; set; } = new();
        public List<FormationDto> Formations { get; set; } = new();
        public List<ProjetDto> Projets { get; set; } = new();
        public List<CompetenceDto> Competences { get; set; } = new();
    }
}
