using Microsoft.EntityFrameworkCore;
using NextStep.Modules.Profile.Domain;
using NextStep.Shared.Persistence;

namespace NextStep.Modules.Profile.Infrastructure.Persistence;

/// <summary>Profile module data: tables of the "profile" schema only.</summary>
public class ProfileDbContext(DbContextOptions<ProfileDbContext> options) : ModuleDbContext(options)
{
    public const string SchemaName = "profile";

    public override string Schema => SchemaName;

    public DbSet<UserEntity> Utilisateurs => Set<UserEntity>();
    public DbSet<Experience> Experiences => Set<Experience>();
    public DbSet<Formation> Formations => Set<Formation>();
    public DbSet<Projet> Projets => Set<Projet>();
    public DbSet<Competence> Competences => Set<Competence>();
    public DbSet<Certification> Certifications => Set<Certification>();
    public DbSet<Keyword> Keywords => Set<Keyword>();

    protected override void ConfigureModel(ModelBuilder modelBuilder)
    {
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
            entity.Property(e => e.ProfileCompleted).HasColumnName("profile_completed");
            entity.Property(e => e.OnboardingStep).HasColumnName("onboarding_step");
            entity.Property(e => e.OnboardingData).HasColumnName("onboarding_data").HasColumnType("jsonb");
            entity.Property(e => e.ProfileScore).HasColumnName("profile_score");
            entity.Property(e => e.DateInscription).HasColumnName("date_inscription");
            entity.HasIndex(e => e.KeycloakId).HasDatabaseName("idx_utilisateur_keycloak_id");
            entity.HasIndex(e => e.Email).HasDatabaseName("idx_utilisateur_email");
        });

        modelBuilder.Entity<Experience>(entity =>
        {
            entity.ToTable("experience");
            entity.Property(e => e.Taches).HasDefaultValueSql("'[]'::jsonb");
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
            entity.HasOne<UserEntity>().WithMany().HasForeignKey(e => e.UserId).OnDelete(DeleteBehavior.Cascade);
        });

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
            entity.HasOne<UserEntity>().WithMany().HasForeignKey(e => e.UserId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Projet>(entity =>
        {
            entity.ToTable("projet");
            entity.Property(e => e.Taches).HasDefaultValueSql("'[]'::jsonb");
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
            entity.HasOne<UserEntity>().WithMany().HasForeignKey(e => e.UserId).OnDelete(DeleteBehavior.Cascade);
        });

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
            entity.HasOne<UserEntity>().WithMany().HasForeignKey(e => e.UserId).OnDelete(DeleteBehavior.Cascade);
        });

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
            entity.HasOne<UserEntity>().WithMany().HasForeignKey(e => e.UserId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Keyword>(entity =>
        {
            entity.ToTable("skill_keyword");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id_skill_keyword").HasDefaultValueSql("gen_random_uuid()");
            entity.Property(e => e.Mot).HasColumnName("mot");
            entity.Property(e => e.Categorie).HasColumnName("categorie").HasDefaultValue("Technique");
            // Case-insensitive uniqueness (lower(mot), categorie) is an expression index: created in the migration.
        });
    }
}
