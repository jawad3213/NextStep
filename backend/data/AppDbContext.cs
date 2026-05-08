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
    public DbSet<UserEmailConnection> UserEmailConnections => Set<UserEmailConnection>();
    public DbSet<OAuthState> OAuthStates => Set<OAuthState>();

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

            entity.Property(e => e.ResponseStatus)
                .HasColumnName("response_status")
                .HasMaxLength(50)
                .HasDefaultValue("EN_ATTENTE");

            entity.Property(e => e.HasResponse)
                .HasColumnName("has_response")
                .HasDefaultValue(false);

            entity.Property(e => e.LastCheckedAtUtc)
                .HasColumnName("last_checked_at_utc");

            entity.Property(e => e.LastResponseAtUtc)
                .HasColumnName("last_response_at_utc");

            entity.Property(e => e.LastResponseFrom)
                .HasColumnName("last_response_from");

            entity.Property(e => e.LastResponseSnippet)
                .HasColumnName("last_response_snippet");

            entity.Property(e => e.ResponseSummary)
                .HasColumnName("response_summary");

            entity.Property(e => e.RecommendedAction)
                .HasColumnName("recommended_action");

            entity.Property(e => e.ResponseConfidence)
                .HasColumnName("response_confidence");

            entity.Property(e => e.ResponseClassifiedAtUtc)
                .HasColumnName("response_classified_at_utc");

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

            entity.Property(e => e.ApprovedAtUtc)
                .HasColumnName("date_approbation");

            entity.Property(e => e.ErrorMessage)
                .HasColumnName("error_message");

            entity.Property(e => e.ProviderMessageId)
                .HasColumnName("provider_message_id")
                .HasMaxLength(255);

            entity.Property(e => e.ProviderThreadId)
                .HasColumnName("provider_thread_id");

            entity.Property(e => e.SendAttemptCount)
                .HasColumnName("nb_tentatives_envoi")
                .HasDefaultValue(0);

            entity.HasOne(e => e.Candidature)
                .WithMany(c => c.EmailDrafts)
                .HasForeignKey(e => e.CandidatureId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // ─── UserEmailConnection ───
        modelBuilder.Entity<UserEmailConnection>(entity =>
        {
            entity.ToTable("user_email_connection");

            entity.HasKey(e => e.Id);

            entity.Property(e => e.Id)
                .HasColumnName("id")
                .HasDefaultValueSql("gen_random_uuid()");

            entity.Property(e => e.UserId)
                .HasColumnName("id_utilisateur")
                .IsRequired();

            entity.Property(e => e.Provider)
                .HasColumnName("provider")
                .HasMaxLength(50)
                .IsRequired();

            entity.Property(e => e.EmailAddress)
                .HasColumnName("adresse_email")
                .HasMaxLength(255)
                .IsRequired();

            entity.Property(e => e.AccessTokenEncrypted)
                .HasColumnName("access_token_chiffre")
                .IsRequired();

            entity.Property(e => e.RefreshTokenEncrypted)
                .HasColumnName("refresh_token_chiffre")
                .IsRequired();

            entity.Property(e => e.AccessTokenExpiresAtUtc)
                .HasColumnName("access_token_expire_utc");

            entity.Property(e => e.CreatedAtUtc)
                .HasColumnName("date_creation")
                .HasDefaultValueSql("now()");

            entity.Property(e => e.UpdatedAtUtc)
                .HasColumnName("date_modification");

            entity.HasIndex(e => new { e.UserId, e.Provider })
                .IsUnique();
        });

        // ─── OAuthState ───
        modelBuilder.Entity<OAuthState>(entity =>
        {
            entity.ToTable("oauth_state");

            entity.HasKey(e => e.Id);

            entity.Property(e => e.Id)
                .HasColumnName("id")
                .HasDefaultValueSql("gen_random_uuid()");

            entity.Property(e => e.UserId)
                .HasColumnName("id_utilisateur")
                .IsRequired();

            entity.Property(e => e.Provider)
                .HasColumnName("provider")
                .HasMaxLength(50)
                .IsRequired();

            entity.Property(e => e.StateTokenHash)
                .HasColumnName("state_token_hash")
                .HasMaxLength(64)
                .IsRequired();

            entity.Property(e => e.ExpiresAtUtc)
                .HasColumnName("expire_utc");

            entity.Property(e => e.Used)
                .HasColumnName("utilise")
                .HasDefaultValue(false);

            entity.Property(e => e.CreatedAtUtc)
                .HasColumnName("date_creation")
                .HasDefaultValueSql("now()");

            entity.Property(e => e.UsedAtUtc)
                .HasColumnName("date_utilisation");

            entity.HasIndex(e => e.StateTokenHash)
                .IsUnique();
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

        // ─── Utilisateur ───
        modelBuilder.Entity<UserEntity>(entity =>
        {
            entity.ToTable("utilisateur");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id_utilisateur");
            entity.Property(e => e.KeycloakId).HasColumnName("keycloak_id");
            entity.Property(e => e.Nom).HasColumnName("nom");
            entity.Property(e => e.Prenom).HasColumnName("prenom");
            entity.Property(e => e.Email).HasColumnName("email");
            entity.Property(e => e.LienLinkedin).HasColumnName("lien_linkedin");
            entity.Property(e => e.LienGithub).HasColumnName("lien_github");
            entity.Property(e => e.LienPortfolio).HasColumnName("lien_portfolio");
            entity.Property(e => e.TitrePoste).HasColumnName("titre_poste");
            entity.Property(e => e.PhotoUrl).HasColumnName("photo_url");
            entity.Property(e => e.Ville).HasColumnName("ville");
            entity.Property(e => e.Pays).HasColumnName("pays");
            entity.Property(e => e.Telephone).HasColumnName("telephone");
            entity.Property(e => e.ResumeProfessionnel).HasColumnName("resume_professionnel");
            entity.Property(e => e.Coordonnees).HasColumnName("coordonnees");
            entity.Property(e => e.TitresSections).HasColumnName("titres_sections").HasColumnType("jsonb");
            entity.Property(e => e.Objectif).HasColumnName("objectif");
            entity.Property(e => e.Niveau).HasColumnName("niveau");
            entity.Property(e => e.Secteur).HasColumnName("secteur");
            entity.Property(e => e.OnboardingCompleted).HasColumnName("onboarding_completed");
            entity.Property(e => e.OnboardingStep).HasColumnName("onboarding_step");
            entity.Property(e => e.OnboardingData).HasColumnName("onboarding_data").HasColumnType("jsonb");
            entity.Property(e => e.ProfileScore).HasColumnName("profile_score");
            entity.Property(e => e.DateInscription).HasColumnName("date_inscription");
        });

        // ─── Experience ───
        modelBuilder.Entity<Experience>(entity =>
        {
            entity.ToTable("experience");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id_experience");
            entity.Property(e => e.UserId).HasColumnName("id_utilisateur");
            entity.Property(e => e.Entreprise).HasColumnName("entreprise");
            entity.Property(e => e.Poste).HasColumnName("poste");
            entity.Property(e => e.DateDebut).HasColumnName("date_debut");
            entity.Property(e => e.DateFin).HasColumnName("date_fin");
            entity.Property(e => e.Missions).HasColumnName("missions");
            entity.Property(e => e.Ville).HasColumnName("ville");
            entity.Property(e => e.TypeContrat).HasColumnName("type_contrat");
            entity.Property(e => e.IsValid).HasColumnName("is_valid");
        });

        // ─── Formation ───
        modelBuilder.Entity<Formation>(entity =>
        {
            entity.ToTable("formation");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id_formation");
            entity.Property(e => e.UserId).HasColumnName("id_utilisateur");
            entity.Property(e => e.Etablissement).HasColumnName("etablissement");
            entity.Property(e => e.Diplome).HasColumnName("diplome");
            entity.Property(e => e.Annee).HasColumnName("annee");
            entity.Property(e => e.AnneeFin).HasColumnName("annee_fin");
            entity.Property(e => e.Ville).HasColumnName("ville");
            entity.Property(e => e.Specialisation).HasColumnName("specialisation");
            entity.Property(e => e.Mention).HasColumnName("mention");
        });

        // ─── Projet ───
        modelBuilder.Entity<Projet>(entity =>
        {
            entity.ToTable("projet");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id_projet");
            entity.Property(e => e.UserId).HasColumnName("id_utilisateur");
            entity.Property(e => e.TitreProjet).HasColumnName("titre_projet");
            entity.Property(e => e.Description).HasColumnName("description");
            entity.Property(e => e.TechnologiesUtilisees).HasColumnName("technologies_utilisees");
            entity.Property(e => e.LienProjet).HasColumnName("lien_projet");
            entity.Property(e => e.DateRealisation).HasColumnName("date_realisation");
            entity.Property(e => e.DemoUrl).HasColumnName("demo_url");
            entity.Property(e => e.ImageUrl).HasColumnName("image_url");
            entity.Property(e => e.IsUniversity).HasColumnName("is_university");
            entity.Property(e => e.IsValid).HasColumnName("is_valid");
        });

        // ─── Competence ───
        modelBuilder.Entity<Competence>(entity =>
        {
            entity.ToTable("competence");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id_competence");
            entity.Property(e => e.UserId).HasColumnName("id_utilisateur");
            entity.Property(e => e.Nom).HasColumnName("nom");
            entity.Property(e => e.Niveau).HasColumnName("niveau");
            entity.Property(e => e.TypeCompetence).HasColumnName("type_competence");
            entity.Property(e => e.IsValid).HasColumnName("is_valid");
        });

        // ─── Certification ───
        modelBuilder.Entity<Certification>(entity =>
        {
            entity.ToTable("certification");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id_certification");
            entity.Property(e => e.UserId).HasColumnName("id_utilisateur");
            entity.Property(e => e.Titre).HasColumnName("titre");
            entity.Property(e => e.Organisation).HasColumnName("organisation");
            entity.Property(e => e.DateObtention).HasColumnName("date_obtention");
            entity.Property(e => e.IdCredential).HasColumnName("id_credential");
            entity.Property(e => e.UrlCredential).HasColumnName("url_credential");
        });

        // ─── Keyword ───
        modelBuilder.Entity<Keyword>(entity =>
        {
            entity.ToTable("skill_keyword");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id_skill_keyword");
            entity.Property(e => e.Mot).HasColumnName("mot");
            entity.Property(e => e.Categorie).HasColumnName("categorie");
        });
    }
}
