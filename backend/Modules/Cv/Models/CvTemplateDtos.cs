namespace NextStep.Modules.Cv.Models;

/// <summary>
/// DTO returned by the template listing / filter endpoint.
/// </summary>
public class CvTemplateDto
{
    public Guid Id { get; set; }
    public string Slug { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? ThumbnailUrl { get; set; }
    public List<string> Industries { get; set; } = new();
    public List<string> ExperienceLevels { get; set; } = new();
    public string Style { get; set; } = string.Empty;
    public List<string> LayoutFlags { get; set; } = new();
    public string BackgroundColor { get; set; } = "#FFFFFF";
    public List<string> Tags { get; set; } = new();
}

/// <summary>
/// Query parameters for filtering the CV template gallery.
/// </summary>
public class CvTemplateFilterQuery
{
    public string? Industry { get; set; }
    public string? ExperienceLevel { get; set; }
    public string? Style { get; set; }
    public string? Layout { get; set; }
    public string? Color { get; set; }
    public string? Tag { get; set; }
}
