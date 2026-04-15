using backend.Modules.Identity.Models;
using Microsoft.EntityFrameworkCore;
using backend.data;

namespace backend.Modules.Identity.Repositories
{
    public interface IUserRepository
    {
        Task<UserEntity?> GetByKeycloakIdAsync(string keycloakId);
        Task CreateUserAsync(UserEntity user);
        Task UpdateUserAsync(UserEntity user);
    }

    public class UserRepository : IUserRepository
    {
        private readonly AppDbContext _context;

        public UserRepository(AppDbContext context)
        {
            _context = context;
        }

        public async Task<UserEntity?> GetByKeycloakIdAsync(string keycloakId)
        {
            return await _context.Set<UserEntity>().FirstOrDefaultAsync(u => u.KeycloakId == keycloakId);
        }

        public async Task CreateUserAsync(UserEntity user)
        {
            await _context.Set<UserEntity>().AddAsync(user);
            await _context.SaveChangesAsync();
        }

        public async Task UpdateUserAsync(UserEntity user)
        {
            _context.Set<UserEntity>().Update(user);
            await _context.SaveChangesAsync();
        }
    }
}
