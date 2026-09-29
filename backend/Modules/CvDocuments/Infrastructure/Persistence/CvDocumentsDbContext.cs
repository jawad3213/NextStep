using Microsoft.EntityFrameworkCore;
using NextStep.Modules.CvDocuments.Domain;
using NextStep.Shared.Persistence;

namespace NextStep.Modules.CvDocuments.Infrastructure.Persistence;

/// <summary>CvDocuments module data: tables of the "cv" schema only.</summary>
public class CvDocumentsDbContext(DbContextOptions<CvDocumentsDbContext> options) : ModuleDbContext(options)
{
    public const string SchemaName = "cv";

    public override string Schema => SchemaName;

    public DbSet<CvTemplate> CvTemplates => Set<CvTemplate>();
    public DbSet<CvHistory> CvHistories => Set<CvHistory>();

    protected override void ConfigureModel(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<CvTemplate>(entity =>
        {
            entity.ToTable("cv_template");

            entity.HasKey(e => e.Id);

            entity.Property(e => e.Id)
                .HasColumnName("id")
                .HasDefaultValueSql("gen_random_uuid()");

            entity.Property(e => e.Slug)
                .HasColumnName("slug")
                .HasMaxLength(50)
                .IsRequired();

            entity.HasIndex(e => e.Slug)
                .IsUnique();

            entity.Property(e => e.Name)
                .HasColumnName("name")
                .HasMaxLength(120)
                .IsRequired();

            entity.Property(e => e.Description)
                .HasColumnName("description")
                .HasMaxLength(500);

            entity.Property(e => e.ThumbnailUrl)
                .HasColumnName("thumbnail_url")
                .HasMaxLength(500);

            entity.Property(e => e.Industries)
                .HasColumnName("industries")
                .HasColumnType("jsonb");

            entity.Property(e => e.ExperienceLevels)
                .HasColumnName("experience_levels")
                .HasColumnType("jsonb");

            entity.Property(e => e.Style)
                .HasColumnName("style")
                .HasConversion<string>()
                .HasMaxLength(30);

            entity.Property(e => e.Layout)
                .HasColumnName("layout");

            entity.Property(e => e.BackgroundColor)
                .HasColumnName("background_color")
                .HasMaxLength(9)
                .HasDefaultValue("#FFFFFF");

            entity.Property(e => e.Tags)
                .HasColumnName("tags")
                .HasColumnType("jsonb");

            entity.Property(e => e.IsActive)
                .HasColumnName("is_active")
                .HasDefaultValue(true);

            entity.Property(e => e.SortOrder)
                .HasColumnName("sort_order")
                .HasDefaultValue(0);

            entity.Property(e => e.CreatedAt)
                .HasColumnName("created_at")
                .HasDefaultValueSql("now()");

            entity.Property(e => e.UpdatedAt)
                .HasColumnName("updated_at");
        });

        modelBuilder.Entity<CvHistory>(entity =>
        {
            entity.ToTable("cv_history");

            entity.HasKey(e => e.Id);

            entity.Property(e => e.Id)
                .HasColumnName("id")
                .HasDefaultValueSql("gen_random_uuid()");

            entity.Property(e => e.UserId)
                .HasColumnName("user_id")
                .IsRequired();

            entity.Property(e => e.Title)
                .HasColumnName("title")
                .HasMaxLength(200);

            entity.Property(e => e.TemplateSlug)
                .HasColumnName("template_slug")
                .HasMaxLength(50)
                .IsRequired();

            entity.Property(e => e.TemplateName)
                .HasColumnName("template_name")
                .HasMaxLength(120);

            entity.Property(e => e.CvDataJson)
                .HasColumnName("cv_data_json")
                .HasColumnType("jsonb")
                .HasDefaultValue("{}");

            entity.Property(e => e.DesignConfigJson)
                .HasColumnName("design_config_json")
                .HasColumnType("jsonb")
                .HasDefaultValue("{}");

            entity.Property(e => e.HtmlSnapshot)
                .HasColumnName("html_snapshot")
                .HasColumnType("text");

            entity.Property(e => e.FileUrl)
                .HasColumnName("file_url")
                .HasMaxLength(1000)
                .IsRequired();

            entity.Property(e => e.ObjectKey)
                .HasColumnName("object_key")
                .HasMaxLength(500)
                .IsRequired();

            entity.Property(e => e.BucketName)
                .HasColumnName("bucket_name")
                .HasMaxLength(100)
                .IsRequired();

            entity.Property(e => e.FileSizeBytes)
                .HasColumnName("file_size_bytes");

            entity.Property(e => e.CreatedAt)
                .HasColumnName("created_at")
                .HasDefaultValueSql("now()");

            entity.Property(e => e.UpdatedAt)
                .HasColumnName("updated_at");

            entity.HasIndex(e => new { e.UserId, e.CreatedAt })
                .HasDatabaseName("ix_cv_history_user_created");
        });
    }
}
