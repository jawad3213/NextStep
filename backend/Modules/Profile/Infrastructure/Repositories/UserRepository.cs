using NextStep.Modules.Profile.Domain;
using Microsoft.EntityFrameworkCore;
using NextStep.Modules.Profile.Infrastructure.Persistence;

namespace NextStep.Modules.Profile.Infrastructure.Repositories {
    public interface IUserRepository
    {
        Task<UserEntity?> GetByKeycloakIdAsync(string keycloakId);
        Task CreateUserAsync(UserEntity user);
        Task UpdateUserAsync(UserEntity user);
    }

    public class UserRepository : IUserRepository
    {
        private readonly ProfileDbContext _context;

        public UserRepository(ProfileDbContext context)
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
