using Microsoft.EntityFrameworkCore;
using NextStep.Modules.Candidature.Models;
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
    }
}

