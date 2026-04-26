// ============================================================
// Infrastructure/Data/AppDbContext.cs
// EF Core DbContext — PostgreSQL
// ============================================================
using Microsoft.EntityFrameworkCore;
using NextStep.Modules.Offer.Models;

namespace NextStep.Infrastructure.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    // ─── M2 : Module Offer ───
    public DbSet<OffreEmploi> OffresEmploi => Set<OffreEmploi>();

    // ─── M1 : à ajouter par M1 ───
    // public DbSet<Utilisateur> Utilisateurs => Set<Utilisateur>();
    // public DbSet<Profil> Profils => Set<Profil>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Schema applicatif
        modelBuilder.HasDefaultSchema("public");

        // ─── OffreEmploi ───
        modelBuilder.Entity<OffreEmploi>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasDefaultValueSql("gen_random_uuid()");
            entity.Property(e => e.AnalyseJson).HasColumnType("jsonb");
            entity.Property(e => e.DateCreation).HasDefaultValueSql("now()");
        });
    }
}
