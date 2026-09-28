namespace NextStep.Modules.Cv.Models;

public class CvDesignConfig
{
    public string ThemeColor { get; set; } = "#1f6feb";
    public string FontFamily { get; set; } = "Inter, Arial, sans-serif";
    public string FontSize { get; set; } = "14px";
    public string LineSpacing { get; set; } = "1.45";
    public string SectionSpacing { get; set; } = "1.2rem";
    public string SidebarWidth { get; set; } = "31%";
}

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
