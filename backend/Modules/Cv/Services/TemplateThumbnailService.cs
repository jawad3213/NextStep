using QuestPDF.Fluent;
using QuestPDF.Infrastructure;
using NextStep.Modules.Cv.Templates;
using PDFtoImage;

namespace NextStep.Modules.Cv.Services;

public interface ITemplateThumbnailService
{
    Task GenerateAllThumbnailsAsync(CancellationToken ct = default);
    byte[]? GetThumbnailPdf(string slug);
    byte[]? GetThumbnailPng(string slug);
    string[] GetTemplateSlugs();
}

public class TemplateThumbnailService : ITemplateThumbnailService
{
    private static readonly string[] Slugs = ["modern", "latex"];
    private readonly string _storageDir;

    public TemplateThumbnailService(IWebHostEnvironment env)
    {
        _storageDir = Path.Combine(env.ContentRootPath, "thumbnails");
        Directory.CreateDirectory(_storageDir);
    }

    public string[] GetTemplateSlugs() => Slugs;

    public async Task GenerateAllThumbnailsAsync(CancellationToken ct = default)
    {
        var data = SampleCvData.Create();
        QuestPDF.Settings.License = LicenseType.Community;

        foreach (var slug in Slugs)
        {
            if (ct.IsCancellationRequested) return;

            try
            {
                var document = CvDocumentFactory.Create(slug, data);
                var pdfBytes = document.GeneratePdf();
                var pdfPath = Path.Combine(_storageDir, $"{slug}.pdf");
                await File.WriteAllBytesAsync(pdfPath, pdfBytes, ct);
                Console.WriteLine($"PDF thumbnail generated: {slug}.pdf ({pdfBytes.Length} bytes)");

                var pngPath = Path.Combine(_storageDir, $"{slug}.png");
                Conversion.SavePng(pngPath, pdfBytes, page: 0);
                Console.WriteLine($"PNG thumbnail generated: {slug}.png");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to generate thumbnail for '{slug}': {ex.Message}");
            }
        }
    }

    public byte[]? GetThumbnailPdf(string slug)
    {
        var filePath = Path.Combine(_storageDir, $"{slug}.pdf");
        if (!File.Exists(filePath)) return null;
        return File.ReadAllBytes(filePath);
    }

    public byte[]? GetThumbnailPng(string slug)
    {
        var filePath = Path.Combine(_storageDir, $"{slug}.png");
        if (!File.Exists(filePath)) return null;
        return File.ReadAllBytes(filePath);
    }
}
