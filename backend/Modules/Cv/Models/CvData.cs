namespace NextStep.Modules.Cv.Models;

/// <summary>
/// Root data object consumed by all CV templates.
/// </summary>
public class CvData
{
    public CvCandidate Candidate { get; set; } = new();
    public string? Summary { get; set; }
    public List<CvExperience> Experience { get; set; } = new();
    public List<CvEducation> Education { get; set; } = new();
    public List<CvSkill> Skills { get; set; } = new();
    public List<CvSkill> TechnicalSkills { get; set; } = new();
    public List<CvSkill> SoftSkills { get; set; } = new();
    public List<CvProject> Projects { get; set; } = new();
    public List<string> Certifications { get; set; } = new();
    public List<string> Languages { get; set; } = new();
    public List<CvActivity> Activities { get; set; } = new();
    public List<CvSection> Sections { get; set; } = new();

    // Customization
    public string? ThemeColor { get; set; }
    public string? FontFamily { get; set; }

    // AI Metadata
    public int AtsScore { get; set; }
    public int MatchingScore { get; set; }
    public double AtsCoveragePct { get; set; }
}

public static class CvSectionTypes
{
    public const string Summary = "summary";
    public const string Experience = "experience";
    public const string Education = "education";
    public const string Skills = "skills";
    public const string SoftSkills = "softskills";
    public const string Projects = "projects";
    public const string Certifications = "certifications";
    public const string Languages = "languages";
    public const string Activities = "activities";
    public const string Accomplishments = "accomplishments";
    public const string Custom = "custom";
}

public static class CvSectionPlacements
{
    public const string Main = "main";
    public const string Sidebar = "sidebar";
}

public class CvSection
{
    public string Id { get; set; } = string.Empty;
    public string Type { get; set; } = CvSectionTypes.Custom;
    public string Title { get; set; } = string.Empty;
    public string Placement { get; set; } = CvSectionPlacements.Main;
    public bool IsVisible { get; set; } = true;
    public int Order { get; set; }
    public string? Text { get; set; }
    public List<CvSectionItem> Items { get; set; } = new();
}

public class CvSectionItem
{
    public string PrimaryText { get; set; } = string.Empty;
    public string SecondaryText { get; set; } = string.Empty;
    public string? StartDate { get; set; }
    public string? EndDate { get; set; }
    public string? Location { get; set; }
    public string? Description { get; set; }
    public int? Level { get; set; }
    public bool IsMatched { get; set; }
    public List<string> Bullets { get; set; } = new();
}

public class CvCandidate
{
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public string? Location { get; set; }
    public string? PhotoUrl { get; set; }
    public string? LinkedIn { get; set; }
    public string? GitHub { get; set; }
    public string? Portfolio { get; set; }
}

public class CvSkill
{
    public string Name { get; set; } = string.Empty;
    public int Level { get; set; } = 1;
    public bool IsMatched { get; set; } = false;
    public string? Category { get; set; }
    public string? TypeCompetence { get; set; }
}

public class CvExperience
{
    public string Role { get; set; } = string.Empty;
    public string Company { get; set; } = string.Empty;
    public string? Start { get; set; }
    public string? End { get; set; }
    public List<string> Bullets { get; set; } = new();
}

public class CvEducation
{
    public string Degree { get; set; } = string.Empty;
    public string Institution { get; set; } = string.Empty;
    public string Year { get; set; } = string.Empty;
    public string? StartYear { get; set; }
    public string? EndYear { get; set; }
}

public class CvProject
{
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public List<string> Technologies { get; set; } = new();
    public string? DateRealisation { get; set; }
    public List<string> Bullets { get; set; } = new();
}

public class CvActivity
{
    public string Title { get; set; } = string.Empty;
    public string? Role { get; set; }
    public string? Description { get; set; }
    public string? StartDate { get; set; }
    public string? EndDate { get; set; }
}
