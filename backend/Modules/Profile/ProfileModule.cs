using NextStep.Modules.Profile.Contracts;
using NextStep.Modules.Profile.Infrastructure.Persistence;
using NextStep.Modules.Profile.Infrastructure.Repositories;
using NextStep.Modules.Profile.Application.Services;
using NextStep.Shared.Persistence;

namespace NextStep.Modules.Profile;

/// <summary>Profile module: users and their candidate profile (schema "profile").</summary>
public static class ProfileModule
{
    public static IServiceCollection AddProfileModule(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddModuleDbContext<ProfileDbContext>(configuration, ProfileDbContext.SchemaName);
        services.AddScoped<IModuleSeeder, SkillKeywordSeeder>();

        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IUserService, UserService>();
        services.AddScoped<IProfileService, ProfileService>();
        services.AddScoped<IProfilePhotoService, ProfilePhotoService>();
        services.AddScoped<IResumeImportService, ResumeImportService>();

        // Public contract
        services.AddScoped<IProfileApi, ProfileApi>();
        return services;
    }
}
