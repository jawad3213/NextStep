using Microsoft.EntityFrameworkCore;
using backend.Modules.Identity.Models;
using NextStep.Modules.Email.Models;
using CandidatureEntity = NextStep.Modules.Candidature.Models.Candidature;
using backend.Modules.Profile.Models;

namespace backend.data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
        {
        }

        public DbSet<UserEntity> Utilisateurs { get; set; }
        public DbSet<CandidatureEntity> Candidatures => Set<CandidatureEntity>();
        public DbSet<EmailDraft> EmailDrafts => Set<EmailDraft>();
         public DbSet<Experience> Experiences { get; set; }
        public DbSet<Formation> Formations { get; set; }
        public DbSet<Projet> Projets { get; set; }
        public DbSet<Competence> Competences { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<CandidatureEntity>(entity =>
            {
                entity.ToTable("candidature");

                entity.HasKey(x => x.IdCandidature);

                entity.Property(x => x.IdCandidature)
                    .HasColumnName("id_candidature");

                entity.Property(x => x.IdUtilisateur)
                    .HasColumnName("id_utilisateur")
                    .IsRequired();

                entity.Property(x => x.IdOffre)
                    .HasColumnName("id_offre")
                    .IsRequired();

                entity.Property(x => x.DateCreation)
                    .HasColumnName("date_creation")
                    .IsRequired();

                entity.Property(x => x.InclureLettreMotivation)
                    .HasColumnName("inclure_lettre_motivation")
                    .IsRequired();

                entity.Property(x => x.Statut)
                    .HasColumnName("statut")
                    .HasMaxLength(50)
                    .IsRequired();
            });

            modelBuilder.Entity<EmailDraft>(entity =>
            {
                entity.ToTable("email_draft");

                entity.HasKey(x => x.Id);

                entity.Property(x => x.Id)
                    .HasColumnName("id_email_draft");

                entity.Property(x => x.CandidatureId)
                    .HasColumnName("id_candidature")
                    .IsRequired();

                entity.Property(x => x.EmailType)
                    .HasColumnName("type_email")
                    .HasMaxLength(50)
                    .IsRequired();

                entity.Property(x => x.Subject)
                    .HasColumnName("objet")
                    .HasMaxLength(255)
                    .IsRequired();

                entity.Property(x => x.Body)
                    .HasColumnName("corps")
                    .IsRequired();

                entity.Property(x => x.Language)
                    .HasColumnName("langue")
                    .HasMaxLength(10)
                    .IsRequired();

                entity.Property(x => x.IsApproved)
                    .HasColumnName("est_approuve")
                    .IsRequired();

                entity.Property(x => x.IsSent)
                    .HasColumnName("est_envoye")
                    .IsRequired();

                entity.Property(x => x.CreatedAtUtc)
                    .HasColumnName("date_creation")
                    .IsRequired();

                entity.HasOne(x => x.Candidature)
                    .WithMany(x => x.EmailDrafts)
                    .HasForeignKey(x => x.CandidatureId)
                    .HasPrincipalKey(x => x.IdCandidature)
                    .OnDelete(DeleteBehavior.Cascade);
            });
        }
       
    }
}