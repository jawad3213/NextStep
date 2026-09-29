using NextStep.Modules.CvDocuments.Application.Services;
using System.Threading;

namespace NextStep.Modules.CvDocuments.Infrastructure.Rendering;

internal static class PuppeteerBrowserDownloadGuard
{
    internal static readonly SemaphoreSlim Lock = new(1, 1);
}

