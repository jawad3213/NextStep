using Microsoft.EntityFrameworkCore;
using NextStep.Modules.Identity.Models;
using NextStep.Modules.Profile.Models;

namespace NextStep.data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
        {
        }

        public DbSet<UserEntity> Utilisateurs { get; set; }
        public DbSet<Experience> Experiences { get; set; }
        public DbSet<Formation> Formations { get; set; }
        public DbSet<Projet> Projets { get; set; }
        public DbSet<Competence> Competences { get; set; }
        public DbSet<Certification> Certifications { get; set; }
        public DbSet<Keyword> Keywords { get; set; }
    }
}
