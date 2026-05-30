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
using NextStep.Modules.Cv.Services;
using NextStep.Modules.Sourcing.Services;
using NextStep.Shared.Storage;
using Amazon.S3;
using System.Linq;
using QuestPDF.Infrastructure;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using Microsoft.Extensions.Options;
using Hangfire;
using Hangfire.PostgreSql;
using NextStep.Jobs;

AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);

var builder = WebApplication.CreateBuilder(args);

builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.AddDebug();

builder.Services.AddControllers();
builder.Services.Configure<ApiBehaviorOptions>(options =>
{
    options.InvalidModelStateResponseFactory = context =>
    {
        var errors = context.ModelState
            .Where(kvp => kvp.Value?.Errors.Count > 0)
            .ToDictionary(
                kvp => kvp.Key,
                kvp => kvp.Value!.Errors.Select(e => e.ErrorMessage).ToArray());

        return new BadRequestObjectResult(new
        {
            error = "Validation failed",
            details = errors
        });
    };
});
builder.Services.AddSignalR();
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
            OnMessageReceived = context =>
            {
                var accessToken = context.Request.Query["access_token"];
                if (!string.IsNullOrEmpty(accessToken))
                    context.Token = accessToken;
                return Task.CompletedTask;
            },
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
builder.Services.Configure<SmtpEmailOptions>(builder.Configuration.GetSection("Email:Smtp"));
builder.Services.AddHttpClient<IAgentHttpClient, AgentHttpClient>();
builder.Services.AddScoped<IOfferRepository, OfferRepository>();
builder.Services.AddScoped<IOfferService, OfferService>();
builder.Services.AddScoped<IPipelineRunnerService, PipelineRunnerService>();
builder.Services.AddScoped<IPdfGenerationService, PdfGenerationService>();
builder.Services.AddScoped<ICandidatureRepository, CandidatureRepository>();
builder.Services.AddScoped<ICandidatureService, CandidatureService>();
builder.Services.AddScoped<IEmailDraftRepository, EmailDraftRepository>();
builder.Services.AddScoped<IEmailService, EmailService>();
builder.Services.AddScoped<IEmailSenderService, GmailEmailSenderService>();
builder.Services.AddScoped<IEmailConnectionService, EmailConnectionService>();
builder.Services.AddScoped<IUserEmailConnectionRepository, UserEmailConnectionRepository>();
builder.Services.AddScoped<IUserOAuthCredentialRepository, UserOAuthCredentialRepository>();
builder.Services.AddScoped<IOAuthStateRepository, OAuthStateRepository>();
builder.Services.AddScoped<IUserRepository, UserRepository>();
builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddScoped<IProfileService, ProfileService>();
builder.Services.AddScoped<IGmailReplyMonitorService, GmailReplyMonitorService>();
builder.Services.AddScoped<IResponseClassificationService, ResponseClassificationService>();
builder.Services.AddScoped<CheckEmailRepliesJob>();
builder.Services.AddScoped<DetectFollowUpNeededJob>();
builder.Services.AddScoped<ICvService, CvService>();
builder.Services.AddScoped<ICvHtmlTemplateRenderer, CvHtmlTemplateRenderer>();
builder.Services.AddScoped<ICvPdfRenderer, CvPdfRenderer>();
builder.Services.AddScoped<ICvTemplateService, CvTemplateService>();
builder.Services.AddScoped<ITemplateThumbnailService, TemplateThumbnailService>();
builder.Services.AddScoped<ISourcedOfferService, SourcedOfferService>();

// ── Google OAuth configuration ───────────────────────────────────────────────
builder.Services.Configure<GoogleOAuthOptions>(
    builder.Configuration.GetSection(GoogleOAuthOptions.SectionName));

// ── Email Follow-up configuration ───────────────────────────────────────────
builder.Services.Configure<EmailFollowUpOptions>(
    builder.Configuration.GetSection(EmailFollowUpOptions.SectionName));

// ── ASP.NET Core Data Protection (encrypts Gmail tokens at rest) ─────────────
builder.Services.AddDataProtection();

// ── Hangfire (reply-monitoring recurring job) ─────────────────────────────────
builder.Services.AddHangfire(config => config
    .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
    .UseSimpleAssemblyNameTypeSerializer()
    .UseRecommendedSerializerSettings()
    .UsePostgreSqlStorage(opt => opt.UseNpgsqlConnection(connectionString)));
builder.Services.AddHangfireServer();

// ─── MinIO / S3 Storage ───
builder.Services.Configure<MinioOptions>(builder.Configuration.GetSection("Minio"));
builder.Services.AddSingleton<IAmazonS3>(sp =>
{
    var opts = sp.GetRequiredService<IOptions<MinioOptions>>().Value;
    var config = new AmazonS3Config
    {
        ServiceURL = opts.Endpoint,
        ForcePathStyle = true,   // Required for MinIO
    };
    return new AmazonS3Client(opts.AccessKey, opts.SecretKey, config);
});
builder.Services.AddSingleton<IStorageService, MinioStorageService>();

QuestPDF.Settings.License = LicenseType.Community;

var app = builder.Build();

if (app.Environment.IsDevelopment()) { app.UseSwagger(); app.UseSwaggerUI(); }
app.UseCors("Angular");
app.UseAuthentication();
app.UseAuthorization();

app.Use(async (ctx, next) =>
{
    try { await next(); }
    catch (Exception ex)
    {
        ctx.Response.StatusCode = 500;
        ctx.Response.ContentType = "application/json";
        await ctx.Response.WriteAsync(
            System.Text.Json.JsonSerializer.Serialize(new { error = ex.Message, type = ex.GetType().Name }));
    }
});

app.MapControllers();
app.MapHub<NextStep.SignalR.PipelineHub>("/hubs/pipeline");

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
    // ── CRITICAL SCHEMA FIX: runs in its own isolated block so it cannot be
    // skipped if any other startup SQL fails. Adds columns that EF Core
    // requires but that may be absent from databases created before the
    // AddCvHtmlRenderingState migration was applied.
    try
    {
        await context.Database.ExecuteSqlRawAsync(
            "ALTER TABLE public.cv_history ADD COLUMN IF NOT EXISTS design_config_json JSONB NOT NULL DEFAULT '{{}}'::jsonb;");
        await context.Database.ExecuteSqlRawAsync(
            "ALTER TABLE public.cv_history ADD COLUMN IF NOT EXISTS html_snapshot TEXT;");
        Console.WriteLine("DEBUG: cv_history schema fix applied.");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"DEBUG: cv_history schema fix skipped (table may not exist yet): {ex.Message}");
    }

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
            CREATE TABLE IF NOT EXISTS public.skill_keyword (id_skill_keyword UUID PRIMARY KEY DEFAULT gen_random_uuid(), mot TEXT NOT NULL);
        ");

        // 2. Force Add Columns (Utilisateur)
        string[] uCols = { "keycloak_id TEXT", "email TEXT", "nom TEXT", "prenom TEXT", "date_inscription TIMESTAMP", "titres_sections JSONB", "objectif TEXT", "niveau TEXT", "secteur TEXT", "onboarding_completed BOOLEAN DEFAULT FALSE", "onboarding_step INTEGER DEFAULT 0", "onboarding_data JSONB", "profile_score INTEGER DEFAULT 0", "ville TEXT", "pays TEXT", "titre_poste TEXT", "photo_url TEXT", "telephone TEXT", "resume_professionnel TEXT", "lien_linkedin TEXT", "lien_github TEXT", "lien_portfolio TEXT", "coordonnees TEXT" };
        foreach (var c in uCols) await context.Database.ExecuteSqlRawAsync($"ALTER TABLE public.utilisateur ADD COLUMN IF NOT EXISTS {c};");

        // 3. Force Add Columns (Experience)
        string[] eCols = { "id_utilisateur UUID", "entreprise TEXT", "poste TEXT", "date_debut TIMESTAMP", "date_fin TIMESTAMP", "missions TEXT", "ville TEXT", "type_contrat TEXT", "taches JSONB NOT NULL DEFAULT '[]'::jsonb", "is_valid BOOLEAN DEFAULT FALSE" };
        foreach (var c in eCols) await context.Database.ExecuteSqlRawAsync($"ALTER TABLE public.experience ADD COLUMN IF NOT EXISTS {c};");

        // 4. Force Add Columns (Formation)
        string[] fCols = { "id_utilisateur UUID", "etablissement TEXT", "diplome TEXT", "annee INTEGER", "ville TEXT", "specialisation TEXT", "mention TEXT", "annee_fin INTEGER" };
        foreach (var c in fCols) await context.Database.ExecuteSqlRawAsync($"ALTER TABLE public.formation ADD COLUMN IF NOT EXISTS {c};");

        // 5. Force Add Columns (Projet)
        string[] pCols = { "id_utilisateur UUID", "titre_projet TEXT", "description TEXT", "technologies_utilisees TEXT", "lien_projet TEXT", "date_realisation TIMESTAMP", "demo_url TEXT", "image_url TEXT", "is_university BOOLEAN DEFAULT FALSE", "taches JSONB NOT NULL DEFAULT '[]'::jsonb", "is_valid BOOLEAN DEFAULT FALSE" };
        foreach (var c in pCols) await context.Database.ExecuteSqlRawAsync($"ALTER TABLE public.projet ADD COLUMN IF NOT EXISTS {c};");

        // 6. Force Add Columns (Competence)
        string[] cCols = { "id_utilisateur UUID", "nom TEXT", "niveau INTEGER", "type_competence TEXT", "is_valid BOOLEAN DEFAULT FALSE" };
        foreach (var c in cCols) await context.Database.ExecuteSqlRawAsync($"ALTER TABLE public.competence ADD COLUMN IF NOT EXISTS {c};");

        // 7. Force Add Columns (Certification)
        string[] ctCols = { "id_utilisateur UUID", "titre TEXT", "organisation TEXT", "date_obtention TIMESTAMP", "id_credential TEXT", "url_credential TEXT" };
        foreach (var c in ctCols) await context.Database.ExecuteSqlRawAsync($"ALTER TABLE public.certification ADD COLUMN IF NOT EXISTS {c};");

        // 8. Force Add Columns (Candidature)
        string[] candCols = { "follow_up_needed BOOLEAN DEFAULT FALSE", "last_follow_up_at_utc TIMESTAMP" };
        foreach (var c in candCols) await context.Database.ExecuteSqlRawAsync($"ALTER TABLE public.candidature ADD COLUMN IF NOT EXISTS {c};");

        await context.Database.ExecuteSqlRawAsync("ALTER TABLE public.skill_keyword ALTER COLUMN id_skill_keyword SET DEFAULT gen_random_uuid();");
        await context.Database.ExecuteSqlRawAsync("ALTER TABLE public.skill_keyword ADD COLUMN IF NOT EXISTS categorie TEXT DEFAULT 'Technique';");
        await context.Database.ExecuteSqlRawAsync("CREATE UNIQUE INDEX IF NOT EXISTS ux_skill_keyword_mot_categorie ON public.skill_keyword (lower(mot), categorie);");
        await context.Database.ExecuteSqlRawAsync(@"
            CREATE TABLE IF NOT EXISTS public.sourced_offer (
                id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
                user_id UUID NOT NULL,
                provider TEXT NOT NULL,
                provider_job_id TEXT NULL,
                external_url TEXT NULL,
                title TEXT NOT NULL,
                company TEXT NULL,
                location TEXT NULL,
                description TEXT NULL,
                posted_at_text TEXT NULL,
                posted_window TEXT NULL,
                raw_contract_type TEXT NULL,
                normalized_contract_type TEXT NULL,
                employment_type TEXT NULL,
                seniority_level TEXT NULL,
                matched_it_terms_json JSONB NOT NULL DEFAULT '[]'::jsonb,
                source_query_json JSONB NOT NULL DEFAULT '{{}}'::jsonb,
                dedupe_key TEXT NOT NULL,
                is_saved BOOLEAN NOT NULL DEFAULT FALSE,
                is_shortlisted BOOLEAN NOT NULL DEFAULT FALSE,
                is_archived BOOLEAN NOT NULL DEFAULT FALSE,
                promoted_offer_id UUID NULL,
                first_seen_at_utc TIMESTAMP NOT NULL DEFAULT now(),
                last_seen_at_utc TIMESTAMP NOT NULL DEFAULT now(),
                scraped_at_utc TIMESTAMP NOT NULL DEFAULT now(),
                created_at_utc TIMESTAMP NOT NULL DEFAULT now(),
                updated_at_utc TIMESTAMP NULL
            );

            CREATE TABLE IF NOT EXISTS public.scrape_session (
                id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
                user_id UUID NOT NULL,
                keywords TEXT NULL,
                location TEXT NULL,
                providers_json JSONB NOT NULL DEFAULT '[]'::jsonb,
                country_code TEXT NULL,
                posted_window TEXT NULL,
                contract_types_json JSONB NOT NULL DEFAULT '[]'::jsonb,
                limit_value INTEGER NOT NULL DEFAULT 20,
                result_count INTEGER NOT NULL DEFAULT 0,
                warnings_json JSONB NOT NULL DEFAULT '[]'::jsonb,
                errors_json JSONB NOT NULL DEFAULT '[]'::jsonb,
                created_at_utc TIMESTAMP NOT NULL DEFAULT now()
            );
        ");
        await context.Database.ExecuteSqlRawAsync("CREATE INDEX IF NOT EXISTS ix_sourced_offer_user_provider_job ON public.sourced_offer (user_id, provider, provider_job_id);");
        await context.Database.ExecuteSqlRawAsync("CREATE INDEX IF NOT EXISTS ix_sourced_offer_user_dedupe ON public.sourced_offer (user_id, dedupe_key);");
        await context.Database.ExecuteSqlRawAsync("CREATE INDEX IF NOT EXISTS ix_scrape_session_user_created ON public.scrape_session (user_id, created_at_utc);");

        // Ensure core recommendations always exist (idempotent).
        await context.Database.ExecuteSqlRawAsync(@"
            INSERT INTO public.skill_keyword (mot, categorie)
            SELECT v.mot, v.categorie
            FROM (VALUES
                -- Programming languages
                ('JavaScript', 'Technique'), ('TypeScript', 'Technique'), ('Python', 'Technique'),
                ('Java', 'Technique'), ('C#', 'Technique'), ('Go', 'Technique'), ('Rust', 'Technique'),
                ('PHP', 'Technique'), ('Kotlin', 'Technique'), ('Swift', 'Technique'),
                -- Interface / Frontend
                ('HTML', 'Interface'), ('CSS', 'Interface'), ('SASS/SCSS', 'Interface'),
                ('Tailwind CSS', 'Interface'), ('Bootstrap', 'Interface'), ('Material UI', 'Interface'),
                ('Angular', 'Interface'), ('React', 'Interface'), ('Vue.js', 'Interface'),
                ('Next.js', 'Interface'), ('Accessibility (a11y)', 'Interface'),
                ('Responsive Design', 'Interface'), ('Figma', 'Interface'),
                -- Infrastructure as Code
                ('Terraform', 'Infrastructure as Code'), ('Pulumi', 'Infrastructure as Code'),
                ('AWS CloudFormation', 'Infrastructure as Code'), ('AWS CDK', 'Infrastructure as Code'),
                ('OpenTofu', 'Infrastructure as Code'), ('Ansible', 'Infrastructure as Code'),
                ('Packer', 'Infrastructure as Code'), ('Vagrant', 'Infrastructure as Code'),
                -- DevOps tools
                ('Docker', 'DevOps'), ('Kubernetes', 'DevOps'), ('Helm', 'DevOps'),
                ('Jenkins', 'DevOps'), ('GitHub Actions', 'DevOps'), ('GitLab CI/CD', 'DevOps'),
                ('ArgoCD', 'DevOps'), ('Prometheus', 'DevOps'), ('Grafana', 'DevOps'),
                ('ELK Stack', 'DevOps'), ('SonarQube', 'DevOps'), ('Trivy', 'DevOps'),
                ('Snyk', 'DevOps'), ('Datadog', 'DevOps'), ('New Relic', 'DevOps')
,
                -- Beginner-friendly modern stack
                ('GitHub', 'Outil'), ('VS Code', 'Outil'), ('npm', 'Outil'),
                ('pnpm', 'Outil'), ('Yarn', 'Outil'),
                ('Vite', 'Interface'), ('Svelte', 'Interface'), ('SvelteKit', 'Interface'),
                ('Nuxt', 'Interface'), ('Astro', 'Interface'),
                ('Redux Toolkit', 'Interface'), ('Zustand', 'Interface'),
                ('TanStack Query', 'Interface'), ('React Router', 'Interface'),
                ('Framer Motion', 'Interface'),
                ('Node.js', 'Technique'), ('Express.js', 'Technique'),
                ('NestJS', 'Technique'), ('FastAPI', 'Technique'),
                ('Prisma', 'Base de donnees'), ('Drizzle ORM', 'Base de donnees'),
                ('Supabase', 'Base de donnees'), ('PlanetScale', 'Base de donnees'),
                ('Neon', 'Base de donnees'),
                ('Redis', 'Base de donnees'),
                ('Firebase Auth', 'Cloud'), ('Cloudflare', 'Cloud'),
                ('Vercel', 'Cloud'), ('Netlify', 'Cloud'),
                ('Playwright', 'Technique'), ('Vitest', 'Technique'),
                ('ESLint', 'Outil'), ('Prettier', 'Outil'),
                ('Docker Compose', 'DevOps'), ('GitHub Codespaces', 'DevOps'),
                ('CI/CD Pipelines', 'DevOps'),
                ('OpenAI API', 'IA'), ('Prompt Engineering', 'IA'),

                -- Full-stack frontend ecosystem
                ('Remix', 'Interface'), ('SolidJS', 'Interface'), ('Qwik', 'Interface'),
                ('Alpine.js', 'Interface'), ('HTMX', 'Interface'), ('jQuery', 'Interface'),
                ('Mantine', 'Interface'), ('Ant Design', 'Interface'), ('PrimeNG', 'Interface'),
                ('PrimeReact', 'Interface'), ('MUI X', 'Interface'),
                ('React Hook Form', 'Interface'), ('Formik', 'Interface'), ('Zod', 'Interface'),
                ('Yup', 'Interface'), ('SWR', 'Interface'), ('Apollo Client', 'Interface'),
                ('PWA', 'Interface'), ('Web Performance Optimization', 'Interface'),
                ('Internationalization (i18n)', 'Interface'), ('Design Systems', 'Interface'),

                -- Backend and API ecosystem
                ('ASP.NET Core', 'Technique'), ('Spring Boot', 'Technique'),
                ('Django', 'Technique'), ('Flask', 'Technique'), ('Laravel', 'Technique'),
                ('Ruby on Rails', 'Technique'), ('Phoenix', 'Technique'),
                ('gRPC', 'Technique'), ('tRPC', 'Technique'), ('OpenAPI/Swagger', 'Technique'),
                ('OAuth2', 'Technique'), ('OpenID Connect', 'Technique'), ('JWT', 'Technique'),
                ('Webhooks', 'Technique'), ('Rate Limiting', 'Technique'),
                ('Background Jobs', 'Technique'), ('Message Queues', 'Technique'),
                ('RabbitMQ', 'Technique'), ('ActiveMQ', 'Technique'),

                -- Data and storage
                ('MariaDB', 'Base de donnees'), ('DynamoDB', 'Base de donnees'),
                ('Cassandra', 'Base de donnees'), ('Couchbase', 'Base de donnees'),
                ('Elasticsearch', 'Base de donnees'), ('TimescaleDB', 'Base de donnees'),
                ('CockroachDB', 'Base de donnees'), ('SQL Optimization', 'Base de donnees'),
                ('Database Migrations', 'Base de donnees'),

                -- Cloud and platform
                ('AWS Lambda', 'Cloud'), ('Amazon ECS', 'Cloud'), ('Amazon EKS', 'Cloud'),
                ('Azure Functions', 'Cloud'), ('Azure DevOps', 'Cloud'),
                ('Google Cloud Run', 'Cloud'), ('Google Kubernetes Engine', 'Cloud'),
                ('Cloudflare Workers', 'Cloud'), ('Serverless Architecture', 'Cloud'),

                -- DevOps and SRE
                ('GitOps', 'DevOps'), ('CI/CD', 'DevOps'),
                ('Infrastructure Monitoring', 'DevOps'), ('Application Logging', 'DevOps'),
                ('Kustomize', 'DevOps'), ('NATS', 'DevOps'),
                ('Blue/Green Deployment', 'DevOps'), ('Canary Deployment', 'DevOps'),
                ('Incident Response', 'DevOps'), ('SLO/SLI', 'DevOps'),

                -- Testing and quality
                ('Unit Testing', 'Technique'), ('Integration Testing', 'Technique'),
                ('End-to-End Testing', 'Technique'), ('API Testing', 'Technique'),
                ('Postman Collections', 'Technique'), ('Contract Testing', 'Technique'),
                ('Test Automation', 'Technique'), ('Code Review', 'Technique'),

                -- Security
                ('OWASP Top 10', 'Security'), ('Secure Coding', 'Security'),
                ('Secrets Management', 'Security'), ('Role-Based Access Control', 'Security'),
                ('Security Testing', 'Security'), ('Dependency Scanning', 'Security'),

                -- Architecture and engineering practices
                ('Monolith', 'Architecture'), ('Microservices', 'Architecture'),
                ('Event-Driven Architecture', 'Architecture'),
                ('Domain-Driven Design', 'Architecture'),
                ('Clean Code', 'Architecture'), ('Refactoring', 'Architecture'),
                ('System Design', 'Architecture'),

                -- Mobile and cross-platform
                ('React Native', 'Framework'), ('Ionic', 'Framework'),
                ('Expo', 'Framework'), ('Capacitor', 'Framework'),

                -- AI engineering
                ('RAG', 'IA'), ('Vector Databases', 'IA'),
                ('LangChain', 'IA'), ('LlamaIndex', 'IA'),
                ('Embeddings', 'IA'), ('LLM Evaluation', 'IA')
            ) AS v(mot, categorie)
            WHERE NOT EXISTS (
                SELECT 1
                FROM public.skill_keyword sk
                WHERE lower(sk.mot) = lower(v.mot) AND sk.categorie = v.categorie
            );
        ");

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

        // 10b. user_oauth_credential — encrypted BYO OAuth client credentials (per user/provider)
        await context.Database.ExecuteSqlRawAsync(@"
            CREATE TABLE IF NOT EXISTS public.user_oauth_credential (
                id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
                id_utilisateur UUID NOT NULL,
                provider TEXT NOT NULL,
                client_id_chiffre TEXT NOT NULL DEFAULT '',
                client_secret_chiffre TEXT NOT NULL DEFAULT '',
                redirect_uri_override TEXT,
                date_creation TIMESTAMP NOT NULL DEFAULT now(),
                date_modification TIMESTAMP,
                UNIQUE (id_utilisateur, provider)
            );
        ");

        // 11. CvTemplate table
        await context.Database.ExecuteSqlRawAsync(@"
            CREATE TABLE IF NOT EXISTS public.cv_template (
                id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
                slug VARCHAR(50) NOT NULL UNIQUE,
                name VARCHAR(120) NOT NULL,
                description VARCHAR(500),
                thumbnail_url VARCHAR(500),
                industries JSONB DEFAULT '[]',
                experience_levels JSONB DEFAULT '[]',
                style VARCHAR(30),
                layout INTEGER DEFAULT 0,
                background_color VARCHAR(9) DEFAULT '#FFFFFF',
                tags JSONB DEFAULT '[]',
                is_active BOOLEAN DEFAULT TRUE,
                sort_order INTEGER DEFAULT 0,
                created_at TIMESTAMP DEFAULT now(),
                updated_at TIMESTAMP
            );
        ");

        // Seed default templates (only those with actual IDocument implementations)
        await context.Database.ExecuteSqlRawAsync(@"
            DELETE FROM public.cv_template;
            INSERT INTO public.cv_template (slug, name, description, thumbnail_url, industries, experience_levels, style, layout, background_color, tags, sort_order)
            VALUES
                ('modern',    'Modern',    'Dark blue header, two-column layout.',         '/api/cv/templates/modern/thumbnail',
                 '[""ITAndEngineering"",""CreativeAndDesign"",""MarketingAndSales""]'::jsonb,
                 '[""MidLevel"",""SeniorExecutive""]'::jsonb,
                 'Modern', 6, '#1B2A4A',
                 '[""two-column"",""dark-header""]'::jsonb, 1),

                ('latex',     'LaTeX Tech','Traditional ATS-friendly classic engineering structure.', '/api/cv/templates/latex/thumbnail',
                 '[""ITAndEngineering"",""EducationAndAcademic""]'::jsonb,
                 '[""MidLevel"",""SeniorExecutive""]'::jsonb,
                 'Traditional', 5, '#FFFFFF',
                 '[""single-column"",""ATS-friendly"",""classic""]'::jsonb, 2);
        ");

        // Update thumbnail_url for existing templates that may have null
        await context.Database.ExecuteSqlRawAsync(@"
            UPDATE public.cv_template
            SET thumbnail_url = '/api/cv/templates/' || slug || '/thumbnail'
            WHERE thumbnail_url IS NULL OR thumbnail_url = '';
        ");

        // 12. CvHistory table
        await context.Database.ExecuteSqlRawAsync(@"
            CREATE TABLE IF NOT EXISTS public.cv_history (
                id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
                user_id UUID NOT NULL,
                title VARCHAR(200),
                template_slug VARCHAR(50) NOT NULL,
                template_name VARCHAR(120),
                cv_data_json JSONB DEFAULT '{{}}',
                design_config_json JSONB NOT NULL DEFAULT '{{}}'::jsonb,
                html_snapshot TEXT,
                file_url VARCHAR(1000) NOT NULL,
                object_key VARCHAR(500) NOT NULL,
                bucket_name VARCHAR(100) NOT NULL,
                file_size_bytes BIGINT DEFAULT 0,
                created_at TIMESTAMP DEFAULT now(),
                updated_at TIMESTAMP
            );
            CREATE INDEX IF NOT EXISTS ix_cv_history_user_created
                ON public.cv_history (user_id, created_at DESC);
        ");

        // Add columns if table already exists (safe idempotent migration)
        string[] histCols = { "title VARCHAR(200)", "cv_data_json JSONB DEFAULT '{{}}'", "updated_at TIMESTAMP", "design_config_json JSONB NOT NULL DEFAULT '{{}}'::jsonb", "html_snapshot TEXT" };
        foreach (var c in histCols)
            await context.Database.ExecuteSqlRawAsync($"ALTER TABLE public.cv_history ADD COLUMN IF NOT EXISTS {c};");

        Console.WriteLine("DEBUG: NUCLEAR REPAIR COMPLETED.");

        // 10. Offers, Candidatures, DocumentGenere and EmailDraft tables
        Console.WriteLine("DEBUG: REPAIRING OFFERS, CANDIDATURES, DOCUMENTS AND EMAILS...");
        await context.Database.ExecuteSqlRawAsync(@"
            CREATE TABLE IF NOT EXISTS public.offres_emploi (
                id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
                texte_brut TEXT,
                analyse_json JSONB DEFAULT '{{}}',
                date_creation TIMESTAMP DEFAULT now(),
                utilisateur_id UUID
            );

            CREATE TABLE IF NOT EXISTS public.candidature (
                id_candidature UUID PRIMARY KEY DEFAULT gen_random_uuid(),
                id_utilisateur UUID,
                id_offre UUID REFERENCES public.offres_emploi(id) ON DELETE CASCADE,
                date_creation TIMESTAMP DEFAULT now(),
                inclure_lettre_motivation BOOLEAN DEFAULT FALSE,
                statut VARCHAR(50) DEFAULT 'EN_ATTENTE'
            );

            CREATE TABLE IF NOT EXISTS public.document_genere (
                id_document UUID PRIMARY KEY DEFAULT gen_random_uuid(),
                id_candidature UUID UNIQUE REFERENCES public.candidature(id_candidature) ON DELETE CASCADE,
                cv_contenu_ia_json JSONB DEFAULT '{{}}',
                lettre_motiv_contenu_ia TEXT,
                chemin_pdf_cv VARCHAR(255),
                chemin_pdf_lettre VARCHAR(255),
                version INTEGER DEFAULT 1,
                date_generation TIMESTAMP DEFAULT now()
            );

            CREATE TABLE IF NOT EXISTS public.email_draft (
                id_email_draft UUID PRIMARY KEY DEFAULT gen_random_uuid(),
                id_candidature UUID REFERENCES public.candidature(id_candidature) ON DELETE CASCADE,
                type_email VARCHAR(50) DEFAULT 'application',
                recipient_email VARCHAR(255),
                objet VARCHAR(255),
                corps TEXT,
                langue VARCHAR(10) DEFAULT 'fr',
                est_approuve BOOLEAN DEFAULT FALSE,
                est_envoye BOOLEAN DEFAULT FALSE,
                date_creation TIMESTAMP DEFAULT now(),
                date_modification TIMESTAMP,
                date_envoi TIMESTAMP,
                error_message TEXT
            );
        ");
        Console.WriteLine("DEBUG: OFFERS, CANDIDATURES, DOCUMENTS AND EMAILS REPAIR COMPLETED.");

        // 11. Ensure MinIO Buckets Exist on Startup
        Console.WriteLine("DEBUG: ENSURING MINIO BUCKETS EXIST...");
        var storageService = scope.ServiceProvider.GetRequiredService<IStorageService>();
        await storageService.EnsureBucketExistsAsync();
        Console.WriteLine("DEBUG: MINIO BUCKETS OK.");

        // 12. Generate template thumbnails only when explicitly enabled.
        var generateThumbnailsOnStartup =
            string.Equals(
                Environment.GetEnvironmentVariable("NEXTSTEP_GENERATE_THUMBNAILS_ON_STARTUP"),
                "true",
                StringComparison.OrdinalIgnoreCase);

        if (generateThumbnailsOnStartup)
        {
            Console.WriteLine("DEBUG: CHECKING TEMPLATE THUMBNAILS...");
            var thumbnailService = scope.ServiceProvider.GetRequiredService<ITemplateThumbnailService>();
            var missing = thumbnailService.GetTemplateSlugs()
                .Where(s => thumbnailService.GetThumbnailPng(s) is null)
                .ToList();
            if (missing.Count > 0)
            {
                Console.WriteLine($"DEBUG: Generating thumbnails for: {string.Join(", ", missing)}");
                await thumbnailService.GenerateAllThumbnailsAsync();
                Console.WriteLine("DEBUG: THUMBNAILS GENERATED.");
            }
            else
            {
                Console.WriteLine("DEBUG: All thumbnails exist. Skipping generation.");
            }
        }
        else
        {
            Console.WriteLine("DEBUG: THUMBNAIL GENERATION DISABLED ON STARTUP.");
        }
    }
    catch (Exception ex) { Console.WriteLine($"DEBUG: REPAIR FAILED: {ex.Message}"); }
}

await app.RunAsync();
