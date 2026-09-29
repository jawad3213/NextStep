using Microsoft.EntityFrameworkCore;
using NextStep.Modules.Applications.Domain;
using NextStep.Shared.Persistence;

namespace NextStep.Modules.Applications.Infrastructure.Persistence;

/// <summary>Applications module data: tables of the "applications" schema only.</summary>
public class ApplicationsDbContext(DbContextOptions<ApplicationsDbContext> options) : ModuleDbContext(options)
{
    public const string SchemaName = "applications";

    public override string Schema => SchemaName;

    public DbSet<OffreEmploi> OffresEmploi => Set<OffreEmploi>();
    public DbSet<Candidature> Candidatures => Set<Candidature>();
    public DbSet<DocumentGenere> DocumentsGeneres => Set<DocumentGenere>();
    public DbSet<CandidatureNote> CandidatureNotes => Set<CandidatureNote>();
    public DbSet<CandidatureStatusHistory> CandidatureStatusHistories => Set<CandidatureStatusHistory>();

    protected override void ConfigureModel(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<OffreEmploi>(entity =>
        {
            entity.ToTable("offres_emploi");

            entity.HasKey(e => e.Id);

            entity.Property(e => e.Id)
                .HasColumnName("id")
                .HasDefaultValueSql("gen_random_uuid()");

            entity.Property(e => e.UtilisateurId)
                .HasColumnName("utilisateur_id");

            entity.HasIndex(e => e.UtilisateurId)
                .HasDatabaseName("ix_offres_emploi_utilisateur_id");

            entity.Property(e => e.TexteBrut)
                .HasColumnName("texte_brut");

            entity.Property(e => e.AnalyseJson)
                .HasColumnName("analyse_json")
                .HasColumnType("jsonb");

            entity.Property(e => e.DateCreation)
                .HasColumnName("date_creation")
                .HasDefaultValueSql("now()");
        });

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
                .HasColumnName("inclure_lettre_motivation")
                .HasDefaultValue(false);

            entity.Property(e => e.Statut)
                .HasColumnName("statut")
                .HasMaxLength(50)
                .HasDefaultValue("ENVOYE");

            // ── Multi-channel tracking ──────────────────────────────────────────
            entity.Property(e => e.Channel)
                .HasColumnName("channel")
                .HasMaxLength(30)
                .HasDefaultValue("EMAIL");

            entity.Property(e => e.ChannelUrl)
                .HasColumnName("channel_url");

            entity.Property(e => e.ChannelContact)
                .HasColumnName("channel_contact")
                .HasMaxLength(255);

            entity.Property(e => e.ApplicationDate)
                .HasColumnName("application_date")
                .HasDefaultValueSql("now()");

            entity.Property(e => e.AppliedManually)
                .HasColumnName("applied_manually")
                .HasDefaultValue(false);

            entity.Property(e => e.OfferSource)
                .HasColumnName("offer_source")
                .HasMaxLength(30);

            entity.Property(e => e.Notes)
                .HasColumnName("notes");

            entity.Property(e => e.Language)
                .HasColumnName("language")
                .HasMaxLength(5)
                .HasDefaultValue("AUTO");

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

            entity.Property(e => e.FollowUpNeeded)
                .HasColumnName("follow_up_needed")
                .HasDefaultValue(false);

            entity.Property(e => e.LastFollowUpAtUtc)
                .HasColumnName("last_follow_up_at_utc");

            entity.HasIndex(e => e.IdOffre)
                .HasDatabaseName("ix_candidature_id_offre");

            // No foreign key to Profile: the user is referenced by id only.
            entity.HasIndex(e => e.IdUtilisateur)
                .HasDatabaseName("ix_candidature_id_utilisateur");

            entity.HasOne(e => e.Offre)
                .WithMany()
                .HasForeignKey(e => e.IdOffre)
                .OnDelete(DeleteBehavior.Cascade);
        });

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

        modelBuilder.Entity<CandidatureNote>(entity =>
        {
            entity.ToTable("candidature_note");

            entity.HasKey(e => e.Id);

            entity.Property(e => e.Id)
                .HasColumnName("id")
                .HasDefaultValueSql("gen_random_uuid()");

            entity.Property(e => e.CandidatureId)
                .HasColumnName("id_candidature")
                .IsRequired();

            entity.Property(e => e.Contenu)
                .HasColumnName("contenu")
                .IsRequired();

            entity.Property(e => e.Auteur)
                .HasColumnName("auteur")
                .HasMaxLength(10)
                .HasDefaultValue("user");

            entity.Property(e => e.CreatedAt)
                .HasColumnName("created_at")
                .HasDefaultValueSql("now()");

            entity.HasIndex(e => e.CandidatureId)
                .HasDatabaseName("ix_candidature_note_candidature");

            entity.HasOne(e => e.Candidature)
                .WithMany(c => c.CandidatureNotes)
                .HasForeignKey(e => e.CandidatureId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<CandidatureStatusHistory>(entity =>
        {
            entity.ToTable("candidature_status_history");

            entity.HasKey(e => e.Id);

            entity.Property(e => e.Id)
                .HasColumnName("id")
                .HasDefaultValueSql("gen_random_uuid()");

            entity.Property(e => e.CandidatureId)
                .HasColumnName("id_candidature")
                .IsRequired();

            entity.Property(e => e.AncienStatut)
                .HasColumnName("ancien_statut")
                .HasMaxLength(50);

            entity.Property(e => e.NouveauStatut)
                .HasColumnName("nouveau_statut")
                .HasMaxLength(50)
                .IsRequired();

            entity.Property(e => e.Source)
                .HasColumnName("source")
                .HasMaxLength(20)
                .HasDefaultValue("user");

            entity.Property(e => e.Details)
                .HasColumnName("details");

            entity.Property(e => e.CreatedAt)
                .HasColumnName("created_at")
                .HasDefaultValueSql("now()");

            entity.HasIndex(e => e.CandidatureId)
                .HasDatabaseName("ix_candidature_status_history_candidature");

            entity.HasOne(e => e.Candidature)
                .WithMany(c => c.StatusHistory)
                .HasForeignKey(e => e.CandidatureId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
