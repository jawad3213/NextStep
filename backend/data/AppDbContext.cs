using Microsoft.EntityFrameworkCore;
using NextStep.Modules.Candidature.Models;
using NextStep.Modules.Cv.Models;
using NextStep.Modules.Email.Models;
using NextStep.Modules.Offer.Models;
using NextStep.Modules.Identity.Models;
using NextStep.Modules.Profile.Models;

namespace NextStep.data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<OffreEmploi> OffresEmploi => Set<OffreEmploi>();
    public DbSet<Candidature> Candidatures => Set<Candidature>();
    public DbSet<EmailDraft> EmailDrafts => Set<EmailDraft>();
    public DbSet<UserEntity> Utilisateurs => Set<UserEntity>();
    public DbSet<Experience> Experiences => Set<Experience>();
    public DbSet<Formation> Formations => Set<Formation>();
    public DbSet<Projet> Projets => Set<Projet>();
    public DbSet<Competence> Competences => Set<Competence>();
    public DbSet<Certification> Certifications => Set<Certification>();
    public DbSet<Keyword> Keywords => Set<Keyword>();
    public DbSet<CvTemplate> CvTemplates => Set<CvTemplate>();
    public DbSet<CvHistory> CvHistories => Set<CvHistory>();
    public DbSet<DocumentGenere> DocumentsGeneres => Set<DocumentGenere>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.HasDefaultSchema("public");

        // ─── OffreEmploi ───
        modelBuilder.Entity<OffreEmploi>(entity =>
        {
            entity.ToTable("offres_emploi");

            entity.HasKey(e => e.Id);

            entity.Property(e => e.Id)
                .HasColumnName("id")
                .HasDefaultValueSql("gen_random_uuid()");

            entity.Property(e => e.UtilisateurId)
                .HasColumnName("utilisateur_id");

            entity.Property(e => e.TexteBrut)
                .HasColumnName("texte_brut");

            entity.Property(e => e.AnalyseJson)
                .HasColumnName("analyse_json")
                .HasColumnType("jsonb");

            entity.Property(e => e.DateCreation)
                .HasColumnName("date_creation")
                .HasDefaultValueSql("now()");
        });

        // ─── Candidature ───
        modelBuilder.Entity<Candidature>(entity =>
        {
            entity.ToTable("candidature");

            entity.HasKey(e => e.IdCandidature);

            entity.Property(e => e.IdCandidature)
                .HasColumnName("id_candidature")
                .HasDefaultValueSql("gen_random_uuid()");

            entity.Property(e => e.IdUtilisateur)
                .HasColumnName("id_utilisateur");

            entity.Property(e => e.IdOffre)
                .HasColumnName("id_offre");

            entity.Property(e => e.DateCreation)
                .HasColumnName("date_creation")
                .HasDefaultValueSql("now()");

            entity.Property(e => e.InclureLettreMotivation)
                .HasColumnName("inclure_lettre_motivation");

            entity.Property(e => e.Statut)
                .HasColumnName("statut")
                .HasMaxLength(50)
                .HasDefaultValue("EN_ATTENTE");

            entity.HasMany(e => e.EmailDrafts)
                .WithOne(e => e.Candidature)
                .HasForeignKey(e => e.CandidatureId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.Offre)
                .WithMany()
                .HasForeignKey(e => e.IdOffre)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // ─── EmailDraft ───
        modelBuilder.Entity<EmailDraft>(entity =>
        {
            entity.ToTable("email_draft");

            entity.HasKey(e => e.Id);

            entity.Property(e => e.Id)
                .HasColumnName("id_email_draft")
                .HasDefaultValueSql("gen_random_uuid()");

            entity.Property(e => e.CandidatureId)
                .HasColumnName("id_candidature")
                .IsRequired();

            entity.Property(e => e.EmailType)
                .HasColumnName("type_email")
                .HasMaxLength(50)
                .HasDefaultValue("application")
                .IsRequired();

            entity.Property(e => e.RecipientEmail)
                .HasColumnName("recipient_email")
                .HasMaxLength(255);

            entity.Property(e => e.Subject)
                .HasColumnName("objet")
                .HasMaxLength(255)
                .IsRequired();

            entity.Property(e => e.Body)
                .HasColumnName("corps")
                .IsRequired();

            entity.Property(e => e.Language)
                .HasColumnName("langue")
                .HasMaxLength(10)
                .HasDefaultValue("fr");

            entity.Property(e => e.IsApproved)
                .HasColumnName("est_approuve")
                .HasDefaultValue(false);

            entity.Property(e => e.IsSent)
                .HasColumnName("est_envoye")
                .HasDefaultValue(false);

            entity.Property(e => e.CreatedAtUtc)
                .HasColumnName("date_creation")
                .HasDefaultValueSql("now()");

            entity.Property(e => e.UpdatedAtUtc)
                .HasColumnName("date_modification");

            entity.Property(e => e.SentAtUtc)
                .HasColumnName("date_envoi");

            entity.Property(e => e.ErrorMessage)
                .HasColumnName("error_message");

            entity.HasOne(e => e.Candidature)
                .WithMany(c => c.EmailDrafts)
                .HasForeignKey(e => e.CandidatureId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // ─── CvTemplate ───
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

        // ─── CvHistory ───
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

        // ─── DocumentGenere ───
        modelBuilder.Entity<DocumentGenere>(entity =>
        {
            entity.ToTable("document_genere");

            entity.HasKey(e => e.IdDocument);

            entity.Property(e => e.IdDocument)
                .HasColumnName("id_document")
                .HasDefaultValueSql("gen_random_uuid()");

            entity.Property(e => e.IdCandidature)
                .HasColumnName("id_candidature")
                .IsRequired();

            entity.HasIndex(e => e.IdCandidature)
                .IsUnique();

            entity.Property(e => e.CvContenuIaJson)
                .HasColumnName("cv_contenu_ia_json")
                .HasColumnType("jsonb");

            entity.Property(e => e.LettreMotivContenuIa)
                .HasColumnName("lettre_motiv_contenu_ia");

            entity.Property(e => e.CheminPdfCv)
                .HasColumnName("chemin_pdf_cv")
                .HasMaxLength(255);

            entity.Property(e => e.CheminPdfLettre)
                .HasColumnName("chemin_pdf_lettre")
                .HasMaxLength(255);

            entity.Property(e => e.Version)
                .HasColumnName("version")
                .HasDefaultValue(1);

            entity.Property(e => e.DateGeneration)
                .HasColumnName("date_generation")
                .HasDefaultValueSql("now()");

            entity.HasOne(e => e.Candidature)
                .WithOne()
                .HasForeignKey<DocumentGenere>(e => e.IdCandidature)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}

