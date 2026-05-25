using PuppeteerSharp;

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
    private readonly ICvHtmlTemplateRenderer _htmlRenderer;
    private readonly ILogger<TemplateThumbnailService> _logger;

    public TemplateThumbnailService(
        IWebHostEnvironment env,
        ICvHtmlTemplateRenderer htmlRenderer,
        ILogger<TemplateThumbnailService> logger)
    {
        _storageDir = Path.Combine(env.ContentRootPath, "thumbnails");
        Directory.CreateDirectory(_storageDir);
        _htmlRenderer = htmlRenderer;
        _logger = logger;
    }

    public string[] GetTemplateSlugs() => Slugs;

    public async Task GenerateAllThumbnailsAsync(CancellationToken ct = default)
    {
        var data = SampleCvData.Create();

        foreach (var slug in Slugs)
        {
            if (ct.IsCancellationRequested) return;

            try
            {
                var rendered = await _htmlRenderer.RenderAsync(slug, data);
                var pngBytes = await RenderHtmlThumbnailAsync(rendered.Html);
                var pngPath = Path.Combine(_storageDir, $"{slug}.html.png");
                await File.WriteAllBytesAsync(pngPath, pngBytes, ct);

                var htmlPath = Path.Combine(_storageDir, $"{slug}.html");
                await File.WriteAllTextAsync(htmlPath, EnsureHtmlDocument(rendered.Html), ct);
                _logger.LogInformation("HTML/CSS thumbnail generated: {Slug}.html.png ({Bytes} bytes)", slug, pngBytes.Length);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to generate HTML/CSS thumbnail for '{Slug}'", slug);
            }
        }
    }

    public byte[]? GetThumbnailPdf(string slug)
    {
        var filePath = Path.Combine(_storageDir, $"{slug}.html.pdf");
        if (!File.Exists(filePath)) return null;
        return File.ReadAllBytes(filePath);
    }

    public byte[]? GetThumbnailPng(string slug)
    {
        var filePath = Path.Combine(_storageDir, $"{slug}.html.png");
        if (!File.Exists(filePath)) return null;
        return File.ReadAllBytes(filePath);
    }

    private async Task<byte[]> RenderHtmlThumbnailAsync(string html)
    {
        var launchOptions = new LaunchOptions
        {
            Headless = true,
            ExecutablePath = ResolveBrowserExecutablePath(),
            Args =
            [
                "--disable-gpu",
                "--disable-dev-shm-usage",
                "--no-sandbox"
            ]
        };

        if (string.IsNullOrWhiteSpace(launchOptions.ExecutablePath))
        {
            launchOptions.ExecutablePath = await EnsureDownloadedBrowserAsync();
        }

        try
        {
            return await ScreenshotWithLaunchOptionsAsync(html, launchOptions);
        }
        catch (Exception ex) when (!string.IsNullOrWhiteSpace(launchOptions.ExecutablePath))
        {
            _logger.LogWarning(ex, "Primary Puppeteer executable failed for thumbnail. Falling back to downloaded Chromium.");
            launchOptions.ExecutablePath = await EnsureDownloadedBrowserAsync();
            return await ScreenshotWithLaunchOptionsAsync(html, launchOptions);
        }
    }

    private static async Task<byte[]> ScreenshotWithLaunchOptionsAsync(string html, LaunchOptions launchOptions)
    {
        await using var browser = await Puppeteer.LaunchAsync(launchOptions);
        await using var page = await browser.NewPageAsync();
        await page.SetViewportAsync(new ViewPortOptions
        {
            Width = 794,
            Height = 1123,
            DeviceScaleFactor = 1
        });
        await page.SetContentAsync(EnsureHtmlDocument(html));
        await page.EmulateMediaTypeAsync(PuppeteerSharp.Media.MediaType.Screen);

        var element = await page.QuerySelectorAsync(".cv-document");
        if (element is not null)
        {
            return await element.ScreenshotDataAsync(new ElementScreenshotOptions
            {
                Type = ScreenshotType.Png,
                OmitBackground = false
            });
        }

        return await page.ScreenshotDataAsync(new ScreenshotOptions
        {
            Type = ScreenshotType.Png,
            FullPage = true,
            OmitBackground = false
        });
    }

    private static string EnsureHtmlDocument(string html)
    {
        if (html.Contains("<html", StringComparison.OrdinalIgnoreCase))
        {
            return html;
        }

        return $$"""
<!DOCTYPE html>
<html lang="en">
<head>
  <meta charset="utf-8">
  <meta name="viewport" content="width=device-width, initial-scale=1">
  <style>
    html, body { margin: 0; padding: 0; background: #eef2f7; }
    body { width: 794px; min-height: 1123px; overflow: hidden; }
    .cv-document { margin: 0 !important; }
    .cv-sheet { box-shadow: none !important; }
  </style>
</head>
<body>
{{html}}
</body>
</html>
""";
    }

    private string? ResolveBrowserExecutablePath()
    {
        var configured = Environment.GetEnvironmentVariable("NEXTSTEP_PUPPETEER_EXECUTABLE_PATH");
        if (OperatingSystem.IsLinux())
        {
            if (!string.IsNullOrWhiteSpace(configured) &&
                File.Exists(configured) &&
                !configured.EndsWith("chromium-browser", StringComparison.OrdinalIgnoreCase))
            {
                return configured;
            }

            _logger.LogInformation("Using Puppeteer-downloaded Chromium on Linux for thumbnails.");
            return null;
        }

        var candidates = new[]
        {
            @"C:\Program Files\Microsoft\Edge\Application\msedge.exe",
            @"C:\Program Files (x86)\Microsoft\Edge\Application\msedge.exe",
            @"C:\Program Files\Google\Chrome\Application\chrome.exe",
            @"C:\Program Files (x86)\Google\Chrome\Application\chrome.exe"
        };

        var match = candidates.FirstOrDefault(File.Exists);
        if (match is null)
        {
            _logger.LogWarning("PuppeteerSharp browser executable not found in default Edge/Chrome paths.");
        }

        return match;
    }

    private static async Task<string> EnsureDownloadedBrowserAsync()
    {
        var browserFetcher = new BrowserFetcher(new BrowserFetcherOptions
        {
            Path = Path.Combine(AppContext.BaseDirectory, ".local-chromium")
        });

        var installed = await browserFetcher.DownloadAsync();
        var executablePath = installed.GetExecutablePath();
        if (string.IsNullOrWhiteSpace(executablePath) || !File.Exists(executablePath))
        {
            throw new InvalidOperationException(
                "PuppeteerSharp could not resolve a usable Chromium executable after download.");
        }

        return executablePath;
    }
}
