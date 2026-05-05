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
    public List<CvProject> Projects { get; set; } = new();
    public List<string> Certifications { get; set; } = new();
    public List<string> Languages { get; set; } = new();
    public List<CvActivity> Activities { get; set; } = new();

    // Customization
    public string? ThemeColor { get; set; }
    public string? FontFamily { get; set; }

    // AI Metadata
    public int AtsScore { get; set; }
    public int MatchingScore { get; set; }
    public double AtsCoveragePct { get; set; }
}

public class CvCandidate
{
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public string? Location { get; set; }
    public string? LinkedIn { get; set; }
    public string? GitHub { get; set; }
    public string? Portfolio { get; set; }
}

public class CvSkill
{
    public string Name { get; set; } = string.Empty;
    public int Level { get; set; } = 1;
    public bool IsMatched { get; set; } = false;
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
}

public class CvProject
{
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public List<string> Bullets { get; set; } = new();
}

public class CvActivity
{
    public string Title { get; set; } = string.Empty;
    public string? Role { get; set; }
    public string? Description { get; set; }
}
