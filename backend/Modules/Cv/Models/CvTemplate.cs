using System.ComponentModel.DataAnnotations;

namespace NextStep.Modules.Cv.Models;

/// <summary>
/// Database entity representing a CV template with its filterable metadata.
/// Each template has a unique slug used as the templateId when generating PDFs.
/// </summary>
public class CvTemplate
{
    public Guid Id { get; set; }

    /// <summary>Unique slug used in URLs and the factory (e.g. "modern", "classic").</summary>
    [MaxLength(50)]
    public string Slug { get; set; } = string.Empty;

    /// <summary>Human-readable display name.</summary>
    [MaxLength(120)]
    public string Name { get; set; } = string.Empty;

    /// <summary>Short description shown in the template picker.</summary>
    [MaxLength(500)]
    public string? Description { get; set; }

    /// <summary>URL or path to the template thumbnail image.</summary>
    [MaxLength(500)]
    public string? ThumbnailUrl { get; set; }

    // ─── Filter attributes ────────────────────────────────────────

    /// <summary>Which industries this template is best suited for.</summary>
    public List<CvIndustry> Industries { get; set; } = new();

    /// <summary>Which experience levels this template targets.</summary>
    public List<CvExperienceLevel> ExperienceLevels { get; set; } = new();

    /// <summary>Visual style category.</summary>
    public CvTemplateStyle Style { get; set; }

    /// <summary>Structural layout flags (one-column, two-column, with/without photo, etc.).</summary>
    public CvTemplateLayout Layout { get; set; }

    /// <summary>
    /// Primary background/accent color hex for the template (e.g. "#1B2A4A").
    /// Allows the user to filter templates by color family.
    /// </summary>
    [MaxLength(9)]
    public string BackgroundColor { get; set; } = "#FFFFFF";

    /// <summary>Free-form tags for extra discoverability (e.g. "ATS-friendly", "minimalist").</summary>
    public List<string> Tags { get; set; } = new();

    /// <summary>Whether this template is published and visible in the picker.</summary>
    public bool IsActive { get; set; } = true;

    /// <summary>Display order for sorting in the template gallery.</summary>
    public int SortOrder { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
}
