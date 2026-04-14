using Microsoft.EntityFrameworkCore;
using backend.Modules.Identity.Models;
using backend.Modules.Email.Models;
using CandidatureEntity = backend.Modules.Candidature.Models.Candidature;


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
          protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<CandidatureEntity>(entity =>
        {
            entity.ToTable("candidatures");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.CompanyName).HasMaxLength(200).IsRequired();
            entity.Property(x => x.JobTitle).HasMaxLength(200).IsRequired();
            entity.Property(x => x.JobOfferText).IsRequired();
            entity.Property(x => x.Status).HasMaxLength(50).IsRequired();
        });

        modelBuilder.Entity<EmailDraft>(entity =>
        {
            entity.ToTable("email_drafts");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.EmailType).HasMaxLength(50).IsRequired();
            entity.Property(x => x.Subject).HasMaxLength(300).IsRequired();
            entity.Property(x => x.Body).IsRequired();
            entity.Property(x => x.Language).HasMaxLength(10).IsRequired();

            entity.HasOne(x => x.Candidature)
                .WithMany(x => x.EmailDrafts)
                .HasForeignKey(x => x.CandidatureId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
    }
}
