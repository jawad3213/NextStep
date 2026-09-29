using NextStep.Modules.Applications.Contracts;
using NextStep.Modules.CvDocuments.Contracts;
using NextStep.Modules.CvDocuments.Infrastructure.Persistence;
using NextStep.Modules.CvDocuments.Application.Services;
using NextStep.Modules.CvDocuments.Infrastructure.Rendering;
using NextStep.Shared.Persistence;

namespace NextStep.Modules.CvDocuments;

/// <summary>CV Documents module: CV templates, history, HTML/PDF rendering (schema "cv").</summary>
public static class CvDocumentsModule
{
    public static IServiceCollection AddCvDocumentsModule(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddModuleDbContext<CvDocumentsDbContext>(configuration, CvDocumentsDbContext.SchemaName);
        services.AddScoped<IModuleSeeder, CvTemplateSeeder>();

        services.AddScoped<ICvService, CvService>();
        services.AddScoped<ICvDraftService, CvDraftService>();
        services.AddScoped<IPdfGenerationService, PdfGenerationService>();
        services.AddScoped<ICvHtmlTemplateRenderer, CvHtmlTemplateRenderer>();
        services.AddScoped<ICvPdfRenderer, CvPdfRenderer>();
        services.AddScoped<ICvTemplateService, CvTemplateService>();
        services.AddScoped<ITemplateThumbnailService, TemplateThumbnailService>();

        // Public contract, and the Applications port this module implements
        services.AddScoped<ICvDocumentsApi, CvDocumentsApi>();
        services.AddScoped<ICvContentSanitizer, CvContentSanitizer>();
        return services;
    }
}
