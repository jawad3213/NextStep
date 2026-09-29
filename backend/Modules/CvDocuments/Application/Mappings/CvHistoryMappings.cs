using NextStep.Modules.CvDocuments.Application.Dtos;
using NextStep.Modules.CvDocuments.Domain;

namespace NextStep.Modules.CvDocuments.Application.Mappings;

public static class CvHistoryMappings
{
    public static CvHistoryDto ToDto(this CvHistory h) => new()
    {
        Id = h.Id,
        Title = h.Title,
        TemplateSlug = h.TemplateSlug,
        TemplateName = h.TemplateName,
        FileUrl = h.FileUrl,
        FileSizeBytes = h.FileSizeBytes,
        CreatedAt = h.CreatedAt,
        UpdatedAt = h.UpdatedAt,
    };
}
