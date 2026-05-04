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

AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddHttpClient();

builder.Services.AddCors(options =>
{
    options.AddPolicy("Angular", policy =>
        policy.WithOrigins("http://localhost:4200").AllowAnyHeader().AllowAnyMethod().AllowCredentials()
    );
});

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.Authority = builder.Configuration["Keycloak:Authority"];
        options.Audience = builder.Configuration["Keycloak:Audience"];
        options.RequireHttpsMetadata = false;
        options.MetadataAddress = "http://keycloak:8080/realms/Next-Step/.well-known/openid-configuration";
        options.TokenValidationParameters = new TokenValidationParameters { ValidateAudience = false, ValidateIssuer = false, NameClaimType = "email" };
        options.Events = new JwtBearerEvents
        {
            OnTokenValidated = async context =>
            {
                var principal = context.Principal;
                if (principal != null)
                {
                    var userService = context.HttpContext.RequestServices.GetRequiredService<IUserService>();
                    try { await userService.EnsureUserCreatedAsync(principal); } catch { }
                }
            }
        };
    });

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
builder.Services.AddDbContext<AppDbContext>(options => {
    options.UseNpgsql(connectionString);
    options.ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.PendingModelChangesWarning));
});

builder.Services.Configure<AgentPythonOptions>(builder.Configuration.GetSection("PythonAgents"));
builder.Services.AddHttpClient<IAgentHttpClient, AgentHttpClient>();
builder.Services.AddScoped<IOfferRepository, OfferRepository>();
builder.Services.AddScoped<IOfferService, OfferService>();
builder.Services.AddScoped<ICandidatureRepository, CandidatureRepository>();
builder.Services.AddScoped<ICandidatureService, CandidatureService>();
builder.Services.AddScoped<IEmailDraftRepository, EmailDraftRepository>();
builder.Services.AddScoped<IEmailService, EmailService>();
builder.Services.AddScoped<IUserRepository, UserRepository>();
builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddScoped<IProfileService, ProfileService>();

var app = builder.Build();

if (app.Environment.IsDevelopment()) { app.UseSwagger(); app.UseSwaggerUI(); }
app.UseCors("Angular");
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    try
    {
        Console.WriteLine("DEBUG: STARTING FULL NUCLEAR REPAIR...");
        
        // 1. Force Create Tables
        await context.Database.ExecuteSqlRawAsync(@"
            CREATE TABLE IF NOT EXISTS public.utilisateur (id_utilisateur UUID PRIMARY KEY);
            CREATE TABLE IF NOT EXISTS public.experience (id_experience UUID PRIMARY KEY);
            CREATE TABLE IF NOT EXISTS public.formation (id_formation UUID PRIMARY KEY);
            CREATE TABLE IF NOT EXISTS public.projet (id_projet UUID PRIMARY KEY);
            CREATE TABLE IF NOT EXISTS public.competence (id_competence UUID PRIMARY KEY);
            CREATE TABLE IF NOT EXISTS public.certification (id_certification UUID PRIMARY KEY);
            CREATE TABLE IF NOT EXISTS public.skill_keyword (id_skill_keyword UUID PRIMARY KEY, mot TEXT NOT NULL);
        ");

        // 2. Force Add Columns (Utilisateur)
        string[] uCols = { "keycloak_id TEXT", "email TEXT", "nom TEXT", "prenom TEXT", "date_inscription TIMESTAMP", "titres_sections JSONB", "objectif TEXT", "niveau TEXT", "secteur TEXT", "onboarding_completed BOOLEAN DEFAULT FALSE", "onboarding_step INTEGER DEFAULT 0", "onboarding_data JSONB", "profile_score INTEGER DEFAULT 0", "ville TEXT", "pays TEXT", "titre_poste TEXT", "photo_url TEXT", "telephone TEXT", "resume_professionnel TEXT", "lien_linkedin TEXT", "lien_github TEXT", "lien_portfolio TEXT", "coordonnees TEXT" };
        foreach (var c in uCols) await context.Database.ExecuteSqlRawAsync($"ALTER TABLE public.utilisateur ADD COLUMN IF NOT EXISTS {c};");

        // 3. Force Add Columns (Experience)
        string[] eCols = { "id_utilisateur UUID", "entreprise TEXT", "poste TEXT", "date_debut TIMESTAMP", "date_fin TIMESTAMP", "missions TEXT", "ville TEXT", "type_contrat TEXT", "is_valid BOOLEAN DEFAULT FALSE" };
        foreach (var c in eCols) await context.Database.ExecuteSqlRawAsync($"ALTER TABLE public.experience ADD COLUMN IF NOT EXISTS {c};");

        // 4. Force Add Columns (Formation)
        string[] fCols = { "id_utilisateur UUID", "etablissement TEXT", "diplome TEXT", "annee INTEGER", "ville TEXT", "specialisation TEXT", "mention TEXT", "annee_fin INTEGER" };
        foreach (var c in fCols) await context.Database.ExecuteSqlRawAsync($"ALTER TABLE public.formation ADD COLUMN IF NOT EXISTS {c};");

        // 5. Force Add Columns (Projet)
        string[] pCols = { "id_utilisateur UUID", "titre_projet TEXT", "description TEXT", "technologies_utilisees TEXT", "lien_projet TEXT", "date_realisation TIMESTAMP", "demo_url TEXT", "image_url TEXT", "is_university BOOLEAN DEFAULT FALSE", "is_valid BOOLEAN DEFAULT FALSE" };
        foreach (var c in pCols) await context.Database.ExecuteSqlRawAsync($"ALTER TABLE public.projet ADD COLUMN IF NOT EXISTS {c};");

        // 6. Force Add Columns (Competence)
        string[] cCols = { "id_utilisateur UUID", "nom TEXT", "niveau INTEGER", "type_competence TEXT", "is_valid BOOLEAN DEFAULT FALSE" };
        foreach (var c in cCols) await context.Database.ExecuteSqlRawAsync($"ALTER TABLE public.competence ADD COLUMN IF NOT EXISTS {c};");

        // 7. Force Add Columns (Certification)
        string[] ctCols = { "id_utilisateur UUID", "titre TEXT", "organisation TEXT", "date_obtention TIMESTAMP", "id_credential TEXT", "url_credential TEXT" };
        foreach (var c in ctCols) await context.Database.ExecuteSqlRawAsync($"ALTER TABLE public.certification ADD COLUMN IF NOT EXISTS {c};");

        await context.Database.ExecuteSqlRawAsync("ALTER TABLE public.skill_keyword ADD COLUMN IF NOT EXISTS categorie TEXT DEFAULT 'Technique';");

        Console.WriteLine("DEBUG: NUCLEAR REPAIR COMPLETED.");
    }
    catch (Exception ex) { Console.WriteLine($"DEBUG: REPAIR FAILED: {ex.Message}"); }
}

await app.RunAsync();