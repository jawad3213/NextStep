using Microsoft.EntityFrameworkCore;
using NextStep.Modules.Candidature.Models;
using NextStep.Modules.Cv.Models;
using NextStep.Modules.Email.Models;
using NextStep.Modules.Offer.Models;
using NextStep.Modules.Identity.Models;
using NextStep.Modules.Profile.Models;
using NextStep.Modules.Sourcing.Models;
using NextStep.Modules.Chatbot.Models;

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
    public DbSet<SourcedOffer> SourcedOffers => Set<SourcedOffer>();
    public DbSet<ScrapeSession> ScrapeSessions => Set<ScrapeSession>();
    
    public DbSet<SessionCoaching> SessionCoachings { get; set; }
    public DbSet<QuestionEntrainement> QuestionEntrainements { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.HasDefaultSchema("public");

        // â”€â”€â”€ OffreEmploi â”€â”€â”€
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

        // â”€â”€â”€ Candidature â”€â”€â”€
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

        // â”€â”€â”€ EmailDraft â”€â”€â”€
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

        // â”€â”€â”€ CvTemplate â”€â”€â”€
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

        // â”€â”€â”€ CvHistory â”€â”€â”€
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

        // â”€â”€â”€ DocumentGenere â”€â”€â”€
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

        modelBuilder.Entity<SourcedOffer>(entity =>
        {
            entity.ToTable("sourced_offer");

            entity.HasKey(e => e.Id);

            entity.Property(e => e.Id)
                .HasColumnName("id")
                .HasDefaultValueSql("gen_random_uuid()");

            entity.Property(e => e.UserId).HasColumnName("user_id");
            entity.Property(e => e.Provider).HasColumnName("provider").HasMaxLength(40).IsRequired();
            entity.Property(e => e.ProviderJobId).HasColumnName("provider_job_id").HasMaxLength(120);
            entity.Property(e => e.ExternalUrl).HasColumnName("external_url").HasMaxLength(1000);
            entity.Property(e => e.Title).HasColumnName("title").HasMaxLength(300).IsRequired();
            entity.Property(e => e.Company).HasColumnName("company").HasMaxLength(300);
            entity.Property(e => e.Location).HasColumnName("location").HasMaxLength(300);
            entity.Property(e => e.Description).HasColumnName("description");
            entity.Property(e => e.PostedAtText).HasColumnName("posted_at_text").HasMaxLength(120);
            entity.Property(e => e.PostedWindow).HasColumnName("posted_window").HasMaxLength(20);
            entity.Property(e => e.RawContractType).HasColumnName("raw_contract_type").HasMaxLength(120);
            entity.Property(e => e.NormalizedContractType).HasColumnName("normalized_contract_type").HasMaxLength(40);
            entity.Property(e => e.EmploymentType).HasColumnName("employment_type").HasMaxLength(120);
            entity.Property(e => e.SeniorityLevel).HasColumnName("seniority_level").HasMaxLength(120);
            entity.Property(e => e.MatchedItTermsJson).HasColumnName("matched_it_terms_json").HasColumnType("jsonb").HasDefaultValue("[]");
            entity.Property(e => e.SourceQueryJson).HasColumnName("source_query_json").HasColumnType("jsonb").HasDefaultValue("{}");
            entity.Property(e => e.DedupeKey).HasColumnName("dedupe_key").HasMaxLength(500).IsRequired();
            entity.Property(e => e.IsSaved).HasColumnName("is_saved").HasDefaultValue(false);
            entity.Property(e => e.IsShortlisted).HasColumnName("is_shortlisted").HasDefaultValue(false);
            entity.Property(e => e.IsArchived).HasColumnName("is_archived").HasDefaultValue(false);
            entity.Property(e => e.PromotedOfferId).HasColumnName("promoted_offer_id");
            entity.Property(e => e.FirstSeenAtUtc).HasColumnName("first_seen_at_utc").HasDefaultValueSql("now()");
            entity.Property(e => e.LastSeenAtUtc).HasColumnName("last_seen_at_utc").HasDefaultValueSql("now()");
            entity.Property(e => e.ScrapedAtUtc).HasColumnName("scraped_at_utc").HasDefaultValueSql("now()");
            entity.Property(e => e.CreatedAtUtc).HasColumnName("created_at_utc").HasDefaultValueSql("now()");
            entity.Property(e => e.UpdatedAtUtc).HasColumnName("updated_at_utc");

            entity.HasIndex(e => new { e.UserId, e.Provider, e.ProviderJobId }).HasDatabaseName("ix_sourced_offer_user_provider_job");
            entity.HasIndex(e => new { e.UserId, e.DedupeKey }).HasDatabaseName("ix_sourced_offer_user_dedupe");
        });

        modelBuilder.Entity<ScrapeSession>(entity =>
        {
            entity.ToTable("scrape_session");

            entity.HasKey(e => e.Id);

            entity.Property(e => e.Id)
                .HasColumnName("id")
                .HasDefaultValueSql("gen_random_uuid()");

            entity.Property(e => e.UserId).HasColumnName("user_id");
            entity.Property(e => e.Keywords).HasColumnName("keywords").HasMaxLength(300);
            entity.Property(e => e.Location).HasColumnName("location").HasMaxLength(300);
            entity.Property(e => e.ProvidersJson).HasColumnName("providers_json").HasColumnType("jsonb").HasDefaultValue("[]");
            entity.Property(e => e.CountryCode).HasColumnName("country_code").HasMaxLength(20);
            entity.Property(e => e.PostedWindow).HasColumnName("posted_window").HasMaxLength(20);
            entity.Property(e => e.ContractTypesJson).HasColumnName("contract_types_json").HasColumnType("jsonb").HasDefaultValue("[]");
            entity.Property(e => e.Limit).HasColumnName("limit_value").HasDefaultValue(20);
            entity.Property(e => e.ResultCount).HasColumnName("result_count").HasDefaultValue(0);
            entity.Property(e => e.WarningsJson).HasColumnName("warnings_json").HasColumnType("jsonb").HasDefaultValue("[]");
            entity.Property(e => e.ErrorsJson).HasColumnName("errors_json").HasColumnType("jsonb").HasDefaultValue("[]");
            entity.Property(e => e.CreatedAtUtc).HasColumnName("created_at_utc").HasDefaultValueSql("now()");

            entity.HasIndex(e => new { e.UserId, e.CreatedAtUtc }).HasDatabaseName("ix_scrape_session_user_created");
        });
    }
}


