using NextStep.Modules.CvDocuments.Domain;

namespace NextStep.Modules.CvDocuments.Application.Dtos;

public class CvRenderRequest
{
    public string TemplateSlug { get; set; } = "modern";
    public CvData Data { get; set; } = new();
    public CvDesignConfig? DesignConfig { get; set; }
}

public class CvRenderResponse
{
    public string TemplateSlug { get; set; } = "modern";
    public CvDesignConfig DesignConfig { get; set; } = new();
    public string Html { get; set; } = string.Empty;
}

public class CvExportPdfRequest
{
    public string TemplateSlug { get; set; } = "modern";
    public CvData Data { get; set; } = new();
    public CvDesignConfig? DesignConfig { get; set; }
    public string? HtmlSnapshot { get; set; }
}
