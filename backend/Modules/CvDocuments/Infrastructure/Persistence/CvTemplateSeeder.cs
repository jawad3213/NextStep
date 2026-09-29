using Microsoft.EntityFrameworkCore;
using NextStep.Modules.CvDocuments.Application.Services;
using NextStep.Modules.CvDocuments.Infrastructure.Rendering;
using NextStep.Shared.Persistence;
using NextStep.Shared.Storage;

namespace NextStep.Modules.CvDocuments.Infrastructure.Persistence;

/// <summary>
/// CV Documents startup data: the template catalogue (only templates with a real renderer),
/// the storage bucket for generated PDFs and, when enabled, the template thumbnails.
/// </summary>
public class CvTemplateSeeder(
    CvDocumentsDbContext db,
    IStorageService storageService,
    ITemplateThumbnailService thumbnailService,
    ILogger<CvTemplateSeeder> logger) : IModuleSeeder
{
    public async Task SeedAsync(CancellationToken ct = default)
    {
        await db.Database.ExecuteSqlRawAsync(@"
            DELETE FROM cv.cv_template;
            INSERT INTO cv.cv_template (slug, name, description, thumbnail_url, industries, experience_levels, style, layout, background_color, tags, sort_order)
            VALUES
                ('modern',    'Modern',    'Dark blue header, two-column layout.',         '/api/cv/templates/modern/thumbnail',
                 '[""ITAndEngineering"",""CreativeAndDesign"",""MarketingAndSales""]'::jsonb,
                 '[""MidLevel"",""SeniorExecutive""]'::jsonb,
                 'Modern', 6, '#1B2A4A',
                 '[""two-column"",""dark-header""]'::jsonb, 1),

                ('latex',     'LaTeX Tech','Traditional ATS-friendly classic engineering structure.', '/api/cv/templates/latex/thumbnail',
                 '[""ITAndEngineering"",""EducationAndAcademic""]'::jsonb,
                 '[""MidLevel"",""SeniorExecutive""]'::jsonb,
                 'Traditional', 5, '#FFFFFF',
                 '[""single-column"",""ATS-friendly"",""classic""]'::jsonb, 2),
                ('executive', 'Executive', 'Elegant single-column serif layout with centred header, for corporate roles.', '/api/cv/templates/executive/thumbnail',
                 '[""FinanceAndConsulting"",""BusinessAndManagement"",""ITAndEngineering""]'::jsonb,
                 '[""MidLevel"",""SeniorExecutive""]'::jsonb,
                 'Elegant', 5, '#FFFFFF',
                 '[""single-column"",""ATS-friendly"",""serif""]'::jsonb, 3),
                ('horizon',   'Horizon',   'Colour header band, timeline experience and a skills side panel.', '/api/cv/templates/horizon/thumbnail',
                 '[""ITAndEngineering"",""CreativeAndDesign"",""MarketingAndSales""]'::jsonb,
                 '[""EntryLevel"",""MidLevel""]'::jsonb,
                 'Modern', 6, '#FFFFFF',
                 '[""two-column"",""timeline"",""header-band""]'::jsonb, 4);
        ", ct);

        // Thumbnail URL for templates that have none
        await db.Database.ExecuteSqlRawAsync(@"
            UPDATE cv.cv_template
            SET thumbnail_url = '/api/cv/templates/' || slug || '/thumbnail'
            WHERE thumbnail_url IS NULL OR thumbnail_url = '';
        ", ct);

        await EnsureStorageAsync();
        await GenerateThumbnailsIfEnabledAsync();
    }

    private async Task EnsureStorageAsync()
    {
        try
        {
            await storageService.EnsureBucketExistsAsync();
            logger.LogInformation("CvDocuments - storage bucket OK");
        }
        catch (Exception ex)
        {
            // PDFs cannot be saved until storage is reachable, but the rest of the app works.
            logger.LogError(ex, "CvDocuments - storage bucket check failed");
        }
    }

    private async Task GenerateThumbnailsIfEnabledAsync()
    {
        var enabled = string.Equals(
            Environment.GetEnvironmentVariable("NEXTSTEP_GENERATE_THUMBNAILS_ON_STARTUP"),
            "true",
            StringComparison.OrdinalIgnoreCase);

        if (!enabled)
        {
            logger.LogInformation("CvDocuments - thumbnail generation disabled on startup");
            return;
        }

        var missing = thumbnailService.GetTemplateSlugs()
            .Where(s => thumbnailService.GetThumbnailPng(s) is null)
            .ToList();

        if (missing.Count == 0)
        {
            logger.LogInformation("CvDocuments - all thumbnails exist");
            return;
        }

        logger.LogInformation("CvDocuments - generating {Count} thumbnails", missing.Count);
        await thumbnailService.GenerateAllThumbnailsAsync();
    }
}
