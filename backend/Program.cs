using NextStep.data;
using Microsoft.EntityFrameworkCore;
using NextStep.Shared.Http;
using NextStep.Shared.Config;
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
using Hangfire;
using Hangfire.PostgreSql;
using NextStep.Jobs;

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
builder.Services.AddScoped<IEmailSenderService, GmailEmailSenderService>();
builder.Services.AddScoped<IEmailConnectionService, EmailConnectionService>();
builder.Services.AddScoped<IUserEmailConnectionRepository, UserEmailConnectionRepository>();
builder.Services.AddScoped<IOAuthStateRepository, OAuthStateRepository>();
builder.Services.AddScoped<IUserRepository, UserRepository>();
builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddScoped<IProfileService, ProfileService>();
builder.Services.AddScoped<IGmailReplyMonitorService, GmailReplyMonitorService>();
builder.Services.AddScoped<IResponseClassificationService, ResponseClassificationService>();
builder.Services.AddScoped<CheckEmailRepliesJob>();
builder.Services.AddScoped<DetectFollowUpNeededJob>();

// ── Google OAuth configuration ───────────────────────────────────────────────
builder.Services.Configure<GoogleOAuthOptions>(
    builder.Configuration.GetSection(GoogleOAuthOptions.SectionName));

// ── Email Follow-up configuration ───────────────────────────────────────────
builder.Services.Configure<EmailFollowUpOptions>(
    builder.Configuration.GetSection(EmailFollowUpOptions.SectionName));

// ── ASP.NET Core Data Protection (encrypts Gmail tokens at rest) ─────────────
// IMPORTANT for Docker: Data Protection keys must be persisted across container
// restarts or encrypted tokens will become unreadable after recreation.
// For production, configure a persistent key store (e.g., keys stored in a
// Docker volume at /root/.aspnet/DataProtection-Keys or an external key store).
builder.Services.AddDataProtection();

// ── Hangfire (reply-monitoring recurring job) ─────────────────────────────────
builder.Services.AddHangfire(config => config
    .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
    .UseSimpleAssemblyNameTypeSerializer()
    .UseRecommendedSerializerSettings()
    .UsePostgreSqlStorage(opt => opt.UseNpgsqlConnection(connectionString)));
builder.Services.AddHangfireServer();

var app = builder.Build();

if (app.Environment.IsDevelopment()) { app.UseSwagger(); app.UseSwaggerUI(); }
app.UseCors("Angular");
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

// ── Hangfire Dashboard (Development only) + Recurring Jobs ───────────────────
if (app.Environment.IsDevelopment())
{
    app.UseHangfireDashboard("/hangfire", new DashboardOptions
    {
        Authorization = new[] { new NextStep.Shared.Http.AllowAllHangfireAuthorizationFilter() }
    });
}

RecurringJob.AddOrUpdate<CheckEmailRepliesJob>(
    "check-email-replies",
    job => job.ExecuteAsync(CancellationToken.None),
    Cron.Daily);   // runs once per day; change to "0 */6 * * *" for every 6 hours

RecurringJob.AddOrUpdate<DetectFollowUpNeededJob>(
    "detect-follow-up-needed",
    job => job.ExecuteAsync(CancellationToken.None),
    Cron.Daily);


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

        // 8. EmailDraft — new columns for approve/send flow
        await context.Database.ExecuteSqlRawAsync("ALTER TABLE public.email_draft ADD COLUMN IF NOT EXISTS date_approbation TIMESTAMP;");
        await context.Database.ExecuteSqlRawAsync("ALTER TABLE public.email_draft ADD COLUMN IF NOT EXISTS provider_message_id TEXT;");
        await context.Database.ExecuteSqlRawAsync("ALTER TABLE public.email_draft ADD COLUMN IF NOT EXISTS nb_tentatives_envoi INTEGER DEFAULT 0;");
        await context.Database.ExecuteSqlRawAsync("ALTER TABLE public.email_draft ADD COLUMN IF NOT EXISTS provider_thread_id TEXT;");

        // 8b. Candidature — email reply tracking fields
        await context.Database.ExecuteSqlRawAsync("ALTER TABLE public.candidature ADD COLUMN IF NOT EXISTS response_status TEXT DEFAULT 'EN_ATTENTE';");
        await context.Database.ExecuteSqlRawAsync("ALTER TABLE public.candidature ADD COLUMN IF NOT EXISTS has_response BOOLEAN DEFAULT FALSE;");
        await context.Database.ExecuteSqlRawAsync("ALTER TABLE public.candidature ADD COLUMN IF NOT EXISTS last_checked_at_utc TIMESTAMP;");
        await context.Database.ExecuteSqlRawAsync("ALTER TABLE public.candidature ADD COLUMN IF NOT EXISTS last_response_at_utc TIMESTAMP;");

        // 8c. Candidature — AI classification fields (Phase 3A)
        await context.Database.ExecuteSqlRawAsync("ALTER TABLE public.candidature ADD COLUMN IF NOT EXISTS last_response_from TEXT;");
        await context.Database.ExecuteSqlRawAsync("ALTER TABLE public.candidature ADD COLUMN IF NOT EXISTS last_response_snippet TEXT;");
        await context.Database.ExecuteSqlRawAsync("ALTER TABLE public.candidature ADD COLUMN IF NOT EXISTS response_summary TEXT;");
        await context.Database.ExecuteSqlRawAsync("ALTER TABLE public.candidature ADD COLUMN IF NOT EXISTS recommended_action TEXT;");
        await context.Database.ExecuteSqlRawAsync("ALTER TABLE public.candidature ADD COLUMN IF NOT EXISTS response_confidence DOUBLE PRECISION;");
        await context.Database.ExecuteSqlRawAsync("ALTER TABLE public.candidature ADD COLUMN IF NOT EXISTS response_classified_at_utc TIMESTAMP;");

        // 9. user_email_connection — stores encrypted Gmail OAuth tokens
        await context.Database.ExecuteSqlRawAsync(@"
            CREATE TABLE IF NOT EXISTS public.user_email_connection (
                id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
                id_utilisateur UUID NOT NULL,
                provider TEXT NOT NULL,
                adresse_email TEXT NOT NULL DEFAULT '',
                access_token_chiffre TEXT NOT NULL DEFAULT '',
                refresh_token_chiffre TEXT NOT NULL DEFAULT '',
                access_token_expire_utc TIMESTAMP NOT NULL DEFAULT now(),
                date_creation TIMESTAMP NOT NULL DEFAULT now(),
                date_modification TIMESTAMP,
                UNIQUE (id_utilisateur, provider)
            );
        ");

        // 10. oauth_state — short-lived single-use CSRF state tokens for OAuth flows
        await context.Database.ExecuteSqlRawAsync(@"
            CREATE TABLE IF NOT EXISTS public.oauth_state (
                id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
                id_utilisateur UUID NOT NULL,
                provider TEXT NOT NULL,
                state_token_hash TEXT NOT NULL UNIQUE,
                expire_utc TIMESTAMP NOT NULL,
                utilise BOOLEAN NOT NULL DEFAULT FALSE,
                date_creation TIMESTAMP NOT NULL DEFAULT now(),
                date_utilisation TIMESTAMP
            );
        ");

        Console.WriteLine("DEBUG: NUCLEAR REPAIR COMPLETED.");
    }
    catch (Exception ex) { Console.WriteLine($"DEBUG: REPAIR FAILED: {ex.Message}"); }
}

await app.RunAsync();