using Microsoft.EntityFrameworkCore;
using backend.Modules.Identity.Models;

namespace backend.data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
        {
        }

        public DbSet<UserEntity> Utilisateurs { get; set; }
    }
}
