using System.ComponentModel.DataAnnotations;

namespace NextStep.Modules.Cv.Models;

/// <summary>
/// Tracks every saved CV. Each record links to the final PDF stored in MinIO
/// and preserves the CvData JSON so the user can reload and continue editing.
/// </summary>
public class CvHistory
{
    public Guid Id { get; set; }

    /// <summary>The user who created this CV.</summary>
    public Guid UserId { get; set; }

    /// <summary>User-given title for this CV (e.g. "Mon CV Marketing 2026").</summary>
    [MaxLength(200)]
    public string? Title { get; set; }

    /// <summary>Template slug used (e.g. "modern", "classic").</summary>
    [MaxLength(50)]
    public string TemplateSlug { get; set; } = string.Empty;

    /// <summary>Human-readable template name at save time.</summary>
    [MaxLength(120)]
    public string? TemplateName { get; set; }

    /// <summary>
    /// Full CvData JSON snapshot — preserves the exact content the user edited.
    /// Allows reloading the CV editor with the same data.
    /// </summary>
    public string CvDataJson { get; set; } = "{}";

    /// <summary>Design configuration snapshot for HTML/CSS rendering.</summary>
    public string DesignConfigJson { get; set; } = "{}";

    /// <summary>Optional final HTML snapshot used for exact reopen/export parity.</summary>
    public string? HtmlSnapshot { get; set; }

    /// <summary>Full URL to the PDF in MinIO.</summary>
    [MaxLength(1000)]
    public string FileUrl { get; set; } = string.Empty;

    /// <summary>Object key inside the MinIO bucket (e.g. "cvs/{userId}/{guid}.pdf").</summary>
    [MaxLength(500)]
    public string ObjectKey { get; set; } = string.Empty;

    /// <summary>MinIO bucket name.</summary>
    [MaxLength(100)]
    public string BucketName { get; set; } = string.Empty;

    /// <summary>File size in bytes.</summary>
    public long FileSizeBytes { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
}
