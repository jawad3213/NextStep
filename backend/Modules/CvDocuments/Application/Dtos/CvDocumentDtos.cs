using NextStep.Modules.CvDocuments.Domain;

namespace NextStep.Modules.CvDocuments.Application.Dtos;

public class CvPreviewResult
{
    public string TemplateSlug { get; set; } = string.Empty;
    public CvData Data { get; set; } = new();
    public CvDesignConfig DesignConfig { get; set; } = new();
    public string Html { get; set; } = string.Empty;
}

public class CvSaveRequest
{
    public string TemplateSlug { get; set; } = string.Empty;
    public string? Title { get; set; }
    public Guid? OfferId { get; set; }
    public CvData Data { get; set; } = new();
    public CvDesignConfig? DesignConfig { get; set; }
    public string? HtmlSnapshot { get; set; }
}

public class CvSaveResult
{
    public Guid HistoryId { get; set; }
    public string FileUrl { get; set; } = string.Empty;
    public long FileSizeBytes { get; set; }
}

public class CvLoadResult
{
    public Guid HistoryId { get; set; }
    public string TemplateSlug { get; set; } = string.Empty;
    public string? TemplateName { get; set; }
    public string? Title { get; set; }
    public CvData Data { get; set; } = new();
    public CvDesignConfig DesignConfig { get; set; } = new();
    public string? HtmlSnapshot { get; set; }
    public string FileUrl { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

public class CvHistoryDto
{
    public Guid Id { get; set; }
    public string? Title { get; set; }
    public string TemplateSlug { get; set; } = string.Empty;
    public string? TemplateName { get; set; }
    public string FileUrl { get; set; } = string.Empty;
    public long FileSizeBytes { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

/// <summary>Where to download a saved CV: <c>{ "downloadUrl": ... }</c>.</summary>
public sealed record CvDownloadResponse(string DownloadUrl);
