using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using backend.data;
using Microsoft.EntityFrameworkCore;
using backend.Modules.Identity.Repositories;
using backend.Modules.Identity.Services;
using backend.Infrastructure.Http;
using backend.Modules.Candidature.Repositories;
using backend.Modules.Candidature.Services;
using backend.Modules.Email.Repositories;
using backend.Modules.Email.Services;

var builder = WebApplication.CreateBuilder(args);

// Fix for Npgsql 6.0+ DateTime Kind=Unspecified issue
AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);

builder.Services.AddControllers();

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(
        builder.Configuration.GetConnectionString("DefaultConnection")
        ?? throw new InvalidOperationException("Connection string 'DefaultConnection' is missing.")
    ));

builder.Services.AddCors(options =>
{
    options.AddPolicy("allowAngular", policy =>
    {
        policy.WithOrigins("http://localhost:4200")
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

// Identity module
builder.Services.AddScoped<IUserRepository, UserRepository>();
builder.Services.AddScoped<IUserService, UserService>(); 

// Module Profile
builder.Services.AddScoped<backend.Modules.Profile.Services.IProfileService, backend.Modules.Profile.Services.ProfileService>();

// Repositories
builder.Services.AddScoped<ICandidatureRepository, CandidatureRepository>();
builder.Services.AddScoped<IEmailDraftRepository, EmailDraftRepository>();

// Services
builder.Services.AddScoped<ICandidatureService, CandidatureService>();
builder.Services.AddScoped<IEmailService, EmailService>();

// HTTP client for Python agents
var pythonAgentsUrl = builder.Configuration["PythonAgents:Url"]
    ?? throw new InvalidOperationException("Configuration 'PythonAgents:Url' is missing.");

builder.Services.AddHttpClient<IAgentHttpClient, AgentHttpClient>(client =>
{
    client.BaseAddress = new Uri(pythonAgentsUrl);
    client.Timeout = TimeSpan.FromSeconds(20);
});

// JWT / Keycloak authentication
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.Authority = builder.Configuration["Keycloak:Authority"];
        options.Audience = builder.Configuration["Keycloak:Audience"];
        options.RequireHttpsMetadata =
            bool.Parse(builder.Configuration["Keycloak:RequireHttpsMetadata"] ?? "false");

        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuers = new[]
            {
                "http://localhost:8080/realms/Next-Step",
                "http://keycloak:8080/realms/Next-Step"
            },
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            NameClaimType = "preferred_username",
            RoleClaimType = "roles"
        };

        options.Events = new JwtBearerEvents
        {
            OnTokenValidated = async context =>
            {
                var principal = context.Principal;
                if (principal != null)
                {
                    var userService = context.HttpContext.RequestServices
                        .GetRequiredService<IUserService>();

                    try
                    {
                        await userService.EnsureUserCreatedAsync(principal);
                    }
                    catch (Exception ex)
                    {
                        var logger = context.HttpContext.RequestServices
                            .GetRequiredService<ILogger<Program>>();

                        logger.LogError(
                            ex,
                            "Erreur lors de la synchronisation JIT de l'utilisateur Keycloak.");
                    }
                }
            }
        };
    });

builder.Services.AddAuthorization();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "NextStep API v1");
    c.RoutePrefix = "swagger";
});

app.UseCors("allowAngular");

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

// Auto-migration on start
using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    try {
        Console.WriteLine("DEBUG: Checking for pending migrations...");
        var pending = await context.Database.GetPendingMigrationsAsync();
        Console.WriteLine($"DEBUG: Pending migrations: {string.Join(", ", pending)}");
        // Fallback: Ensure columns exist manually
        await context.Database.ExecuteSqlRawAsync("ALTER TABLE utilisateur ADD COLUMN IF NOT EXISTS objectif TEXT;");
        await context.Database.ExecuteSqlRawAsync("ALTER TABLE utilisateur ADD COLUMN IF NOT EXISTS niveau TEXT;");
        await context.Database.ExecuteSqlRawAsync("ALTER TABLE utilisateur ADD COLUMN IF NOT EXISTS secteur TEXT;");
        await context.Database.ExecuteSqlRawAsync("ALTER TABLE utilisateur ADD COLUMN IF NOT EXISTS onboarding_completed BOOLEAN DEFAULT FALSE;");
        await context.Database.ExecuteSqlRawAsync("ALTER TABLE utilisateur ADD COLUMN IF NOT EXISTS onboarding_step INTEGER DEFAULT 0;");
        await context.Database.ExecuteSqlRawAsync("ALTER TABLE utilisateur ADD COLUMN IF NOT EXISTS onboarding_data JSONB;");
        await context.Database.ExecuteSqlRawAsync("ALTER TABLE utilisateur ADD COLUMN IF NOT EXISTS profile_score INTEGER DEFAULT 0;");
        
        await context.Database.MigrateAsync();
        Console.WriteLine("DEBUG: Migrations/SQL applied successfully!");
    } catch (Exception ex) {
        Console.WriteLine($"DEBUG: Migration error: {ex.Message}");
    }
}

await app.RunAsync();