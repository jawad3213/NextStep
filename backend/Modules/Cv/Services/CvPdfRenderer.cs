using PuppeteerSharp;

namespace NextStep.Modules.Cv.Services;

public interface ICvPdfRenderer
{
    Task<byte[]> RenderPdfAsync(string html);
}

public class CvPdfRenderer(ILogger<CvPdfRenderer> logger) : ICvPdfRenderer
{
    public async Task<byte[]> RenderPdfAsync(string html)
    {
        var launchOptions = new LaunchOptions
        {
            Headless = true,
            ExecutablePath = ResolveBrowserExecutablePath(),
            Args = new[]
            {
                "--disable-gpu",
                "--disable-dev-shm-usage",
                "--no-sandbox"
            }
        };

        if (string.IsNullOrWhiteSpace(launchOptions.ExecutablePath))
        {
            launchOptions.ExecutablePath = await EnsureDownloadedBrowserAsync();
        }

        try
        {
            return await RenderWithLaunchOptionsAsync(html, launchOptions);
        }
        catch (Exception ex) when (!string.IsNullOrWhiteSpace(launchOptions.ExecutablePath))
        {
            logger.LogWarning(ex, "Primary Puppeteer executable failed. Falling back to a downloaded Chromium binary.");
            launchOptions.ExecutablePath = await EnsureDownloadedBrowserAsync();
            return await RenderWithLaunchOptionsAsync(html, launchOptions);
        }
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
</head>
<body style="margin:0; background:#eef2f7;">
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

            logger.LogInformation("Using Puppeteer-downloaded Chromium on Linux.");
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
            logger.LogWarning("PuppeteerSharp browser executable not found in default Edge/Chrome paths.");
        }

        return match;
    }

    private async Task<byte[]> RenderWithLaunchOptionsAsync(string html, LaunchOptions launchOptions)
    {
        await using var browser = await Puppeteer.LaunchAsync(launchOptions);
        await using var page = await browser.NewPageAsync();
        await page.SetContentAsync(EnsureHtmlDocument(html));
        await page.EmulateMediaTypeAsync(PuppeteerSharp.Media.MediaType.Print);

        return await page.PdfDataAsync(new PdfOptions
        {
            Format = PuppeteerSharp.Media.PaperFormat.A4,
            PrintBackground = true,
            PreferCSSPageSize = true,
            MarginOptions = new PuppeteerSharp.Media.MarginOptions
            {
                Top = "0",
                Bottom = "0",
                Left = "0",
                Right = "0"
            }
        });
    }

    private async Task<string> EnsureDownloadedBrowserAsync()
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
