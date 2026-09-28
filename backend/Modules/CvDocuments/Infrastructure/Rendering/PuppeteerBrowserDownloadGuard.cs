using System.Threading;

namespace NextStep.Modules.Cv.Services;

internal static class PuppeteerBrowserDownloadGuard
{
    internal static readonly SemaphoreSlim Lock = new(1, 1);
}

