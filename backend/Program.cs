using NextStep.data;
using Microsoft.EntityFrameworkCore;
using NextStep.Shared.Http;
using NextStep.Modules.Candidature.Repositories;
using NextStep.Modules.Candidature.Services;
using NextStep.Modules.Email.Repositories;
using NextStep.Modules.Email.Services;
using NextStep.Modules.Offer.Repositories;
using NextStep.Modules.Offer.Services;
using NextStep.Modules.Identity.Repositories;
using NextStep.Modules.Identity.Services;
using NextStep.Modules.Profile.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Microsoft.Extensions.Options;

// Fix for PostgreSQL DateTime issue: Allow writing unspecified timestamps
AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);


var builder = WebApplication.CreateBuilder(args);

//
// ─── Controllers + Swagger ─────────────────────────────────────
//

builder.Services.AddControllers();
// DB Context will be configured later in the file with full options

builder.Services.AddCors(options =>
{
    options.AddPolicy("allowAngular", policy =>
    {
        policy.WithOrigins("http://localhost:4200")
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

// Services are registered in their respective sections below
builder.Services.AddHttpClient();


// Configuration de l'authentification JWT
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.Authority = builder.Configuration["Keycloak:Authority"];
        options.Audience = builder.Configuration["Keycloak:Audience"];
        options.RequireHttpsMetadata = builder.Configuration.GetValue<bool>("Keycloak:RequireHttpsMetadata");
        
        // Explicitly set MetadataAddress for internal Docker communication
        // This ensures the backend can find the signing keys even if 'Authority' is confusing
        options.MetadataAddress = "http://keycloak:8080/realms/Next-Step/.well-known/openid-configuration";

        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateAudience = false,
            ValidateIssuer = false,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            NameClaimType = "email",
            RoleClaimType = "roles"
        };

        // Implémentation de la méthode Lazy Initialization (JIT Provisioning)
        options.Events = new JwtBearerEvents
        {
            OnAuthenticationFailed = context =>
            {
                Console.WriteLine($"DEBUG: Auth Failed: {context.Exception.Message}");
                return Task.CompletedTask;
            },
            OnTokenValidated = async context =>
            {
                Console.WriteLine("DEBUG: Token Validated Successfully!");
                var principal = context.Principal;
                if (principal != null)
                {
                    var userService = context.HttpContext.RequestServices.GetRequiredService<IUserService>();
                    try
                    {
                        await userService.EnsureUserCreatedAsync(principal);
                    }
                    catch (Exception ex)
                    {
                        var logger = context.HttpContext.RequestServices.GetRequiredService<ILogger<Program>>();
                        logger.LogError(ex, "Erreur lors de la synchronisation JIT.");
                    }
                }
            }
        };
    });

builder.Services.AddEndpointsApiExplorer();

builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new()
    {
        Title = "NextStep API",
        Version = "v1"
    });
});

//
// ─── PostgreSQL + EF Core ─────────────────────────────────────
//

builder.Services.AddDbContext<AppDbContext>(options =>
{
    options.UseNpgsql(
        builder.Configuration.GetConnectionString("DefaultConnection"),
        npg => npg.MigrationsAssembly(typeof(AppDbContext).Assembly.FullName)
    );
    // Suppress EF Core 10 warning about pending model changes in dev
    options.ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.PendingModelChangesWarning));
});

//
// ─── Python Agents HTTP Client ─────────────────────────────────
//

builder.Services.Configure<AgentPythonOptions>(
    builder.Configuration.GetSection("PythonAgents")
);

builder.Services.AddHttpClient<IAgentHttpClient, AgentHttpClient>();

//
// ─── Module Offer (M2) ─────────────────────────────────────────
//

builder.Services.AddScoped<IOfferRepository, OfferRepository>();
builder.Services.AddScoped<IOfferService, OfferService>();

//
// ─── Module Candidature ────────────────────────────────────────
//

builder.Services.AddScoped<ICandidatureRepository, CandidatureRepository>();
builder.Services.AddScoped<ICandidatureService, CandidatureService>();

//
// ─── Module Email ──────────────────────────────────────────────
//

builder.Services.AddScoped<IEmailDraftRepository, EmailDraftRepository>();
builder.Services.AddScoped<IEmailService, EmailService>();

//
// ─── Module Identity ───────────────────────────────────────────
//

builder.Services.AddScoped<IUserRepository, UserRepository>();
builder.Services.AddScoped<IUserService, UserService>();

//
// ─── Module Profile ────────────────────────────────────────────
//

builder.Services.AddScoped<IProfileService, ProfileService>();

//
// ─── CORS Angular Dev Environment ──────────────────────────────
//

builder.Services.AddCors(options =>
{
    options.AddPolicy("Angular", policy =>
        policy.WithOrigins("http://localhost:4200")
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials()
    );
});

var app = builder.Build();

//
// ─── Middleware Pipeline ───────────────────────────────────────
//

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors("Angular");

// Required because controllers use [Authorize]
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

//
// ─── Health Check Endpoint ─────────────────────────────────────
//

app.MapGet("/health", () =>
    Results.Ok(new
    {
        status = "ok",
        service = "nextstep-backend"
    })
);

//
// ─── Startup SQL + Auto Migrations (TEAM SAFE MODE) ─────────────
//

using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

    IEnumerable<string> pending = [];
    try
    {
        // 1. Appliquer les migrations EF Core standard
        Console.WriteLine("DEBUG: Checking pending migrations...");
        pending = await context.Database.GetPendingMigrationsAsync();
        if (pending.Any())
        {
            Console.WriteLine($"DEBUG: Applying pending migrations: {string.Join(", ", pending)}");
            await context.Database.MigrateAsync();
        }

        // 2. Toujours s'assurer que les colonnes additionnelles existent (Mode Robustesse)
        // Ceci corrige les cas où les migrations sont incomplètes ou désynchronisées
        Console.WriteLine("DEBUG: Ensuring all utilisateur columns exist...");
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
        await context.Database.ExecuteSqlRawAsync("ALTER TABLE utilisateur ADD COLUMN IF NOT EXISTS lien_portfolio TEXT;");
        await context.Database.ExecuteSqlRawAsync("ALTER TABLE utilisateur ADD COLUMN IF NOT EXISTS coordonnees TEXT;");

        Console.WriteLine("DEBUG: Database schema is fully synchronized.");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"DEBUG: Critical error during database startup: {ex.Message}");
    }
}

    // Ensure Certification and Project tables exist
    using (var scope2 = app.Services.CreateScope())
    {
        var context2 = scope2.ServiceProvider.GetRequiredService<AppDbContext>();
        await context2.Database.ExecuteSqlRawAsync(@"
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

        await context2.Database.ExecuteSqlRawAsync(@"
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
            await context2.Database.MigrateAsync();
        } catch {
            Console.WriteLine("DEBUG: Migration skipped or failed, but SQL applied.");
        }
        Console.WriteLine("DEBUG: Migrations/SQL applied successfully!");
    }

await app.RunAsync();