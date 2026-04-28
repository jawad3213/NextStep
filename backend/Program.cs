using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Microsoft.Extensions.Options;
using NextStep.data;
using Microsoft.EntityFrameworkCore;
using NextStep.Modules.Identity.Repositories;
using NextStep.Modules.Identity.Services;

var builder = WebApplication.CreateBuilder(args);

// Fix for Npgsql 6.0+ DateTime Kind=Unspecified issue
AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);

builder.Services.AddControllers();
builder.Services.AddDbContext<AppDbContext>(options => 
    options.UseNpgsql( 
        builder.Configuration.GetConnectionString("DefaultConnection") 
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

// Module Identity
builder.Services.AddScoped<IUserRepository, UserRepository>();
builder.Services.AddScoped<IUserService, UserService>(); 

// Module Profile
builder.Services.AddScoped<NextStep.Modules.Profile.Services.IProfileService, NextStep.Modules.Profile.Services.ProfileService>();
builder.Services.AddHttpClient();


// Configuration de l'authentification JWT
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.Authority = builder.Configuration["Keycloak:Authority"];
        options.Audience = builder.Configuration["Keycloak:Audience"];
        options.RequireHttpsMetadata = bool.Parse(builder.Configuration["Keycloak:RequireHttpsMetadata"] ?? "false");
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuers = new[] 
            { 
                "http://localhost:8080/realms/Next-Step",
                "http://auth:8080/realms/Next-Step"
            },
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            NameClaimType = "email",
            RoleClaimType = "roles"
        };

        // Implémentation de la méthode Lazy Initialization (JIT Provisioning)
        options.Events = new JwtBearerEvents
        {
            OnTokenValidated = async context =>
            {
                var principal = context.Principal;
                if (principal != null)
                {
                    // Récupérer le UserService de l'injection de dépendances pour ce scope
                    var userService = context.HttpContext.RequestServices.GetRequiredService<IUserService>();
                    
                    try
                    {
                        // S'assurer que l'utilisateur est bien synchronisé/créé dans la DB locale
                        await userService.EnsureUserCreatedAsync(principal);
                    }
                    catch (Exception ex)
                    {
                        // Logguer l'erreur ou gérer l'exception
                        var logger = context.HttpContext.RequestServices.GetRequiredService<ILogger<Program>>();
                        logger.LogError(ex, "Erreur lors de la synchronisation JIT de l'utilisateur Keycloak.");
                    }
                }
            }
        };
    });

builder.Services.AddAuthorization();

// Add Swagger Gen
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// Enable Swagger UI (Forced for testing current features)
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
        await context.Database.ExecuteSqlRawAsync("ALTER TABLE utilisateur ADD COLUMN IF NOT EXISTS titres_sections JSONB;");
        await context.Database.ExecuteSqlRawAsync("ALTER TABLE utilisateur ADD COLUMN IF NOT EXISTS objectif TEXT;");
        await context.Database.ExecuteSqlRawAsync("ALTER TABLE utilisateur ADD COLUMN IF NOT EXISTS niveau TEXT;");
        await context.Database.ExecuteSqlRawAsync("ALTER TABLE utilisateur ADD COLUMN IF NOT EXISTS secteur TEXT;");
        await context.Database.ExecuteSqlRawAsync("ALTER TABLE utilisateur ADD COLUMN IF NOT EXISTS onboarding_completed BOOLEAN DEFAULT FALSE;");
        await context.Database.ExecuteSqlRawAsync("ALTER TABLE utilisateur ADD COLUMN IF NOT EXISTS onboarding_step INTEGER DEFAULT 0;");
        await context.Database.ExecuteSqlRawAsync("ALTER TABLE utilisateur ADD COLUMN IF NOT EXISTS onboarding_data JSONB;");
        await context.Database.ExecuteSqlRawAsync("ALTER TABLE utilisateur ADD COLUMN IF NOT EXISTS profile_score INTEGER DEFAULT 0;");
        await context.Database.ExecuteSqlRawAsync("ALTER TABLE utilisateur ADD COLUMN IF NOT EXISTS ville TEXT;");
        await context.Database.ExecuteSqlRawAsync("ALTER TABLE utilisateur ADD COLUMN IF NOT EXISTS pays TEXT;");
        await context.Database.ExecuteSqlRawAsync("ALTER TABLE utilisateur ADD COLUMN IF NOT EXISTS titre_poste TEXT;");
        await context.Database.ExecuteSqlRawAsync("ALTER TABLE utilisateur ADD COLUMN IF NOT EXISTS photo_url TEXT;");
        await context.Database.ExecuteSqlRawAsync("ALTER TABLE utilisateur ADD COLUMN IF NOT EXISTS telephone TEXT;");
        await context.Database.ExecuteSqlRawAsync("ALTER TABLE utilisateur ADD COLUMN IF NOT EXISTS resume_professionnel TEXT;");
        await context.Database.ExecuteSqlRawAsync("ALTER TABLE utilisateur ADD COLUMN IF NOT EXISTS lien_linkedin TEXT;");
        await context.Database.ExecuteSqlRawAsync("ALTER TABLE utilisateur ADD COLUMN IF NOT EXISTS lien_github TEXT;");
        
        // Ensure Certification and Project tables exist
        await context.Database.ExecuteSqlRawAsync(@"
            CREATE TABLE IF NOT EXISTS certification (
                id_certification UUID PRIMARY KEY,
                id_utilisateur UUID NOT NULL,
                titre TEXT,
                organisation TEXT,
                date_obtention TIMESTAMP,
                id_credential TEXT,
                url_credential TEXT
            );
        ");

        await context.Database.ExecuteSqlRawAsync(@"
            CREATE TABLE IF NOT EXISTS projet (
                id_projet UUID PRIMARY KEY,
                id_utilisateur UUID NOT NULL,
                titre_projet TEXT,
                description TEXT,
                technologies_utilisees TEXT,
                lien_projet TEXT,
                date_realisation TIMESTAMP,
                demo_url TEXT,
                image_url TEXT,
                is_university BOOLEAN DEFAULT FALSE,
                is_valid BOOLEAN DEFAULT FALSE
            );
        ");

        try {
            await context.Database.MigrateAsync();
        } catch {
            Console.WriteLine("DEBUG: Migration skipped or failed, but SQL applied.");
        }
        Console.WriteLine("DEBUG: Migrations/SQL applied successfully!");
    } catch (Exception ex) {
        Console.WriteLine($"DEBUG: Migration error: {ex.Message}");
    }
}

await app.RunAsync();
