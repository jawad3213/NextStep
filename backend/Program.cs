using Microsoft.EntityFrameworkCore;
using NextStep.Infrastructure.Data;
using NextStep.Infrastructure.Http;
using NextStep.Modules.Candidature.Repositories;
using NextStep.Modules.Candidature.Services;
using NextStep.Modules.Email.Repositories;
using NextStep.Modules.Email.Services;
using NextStep.Modules.Offer.Repositories;
using NextStep.Modules.Offer.Services;

var builder = WebApplication.CreateBuilder(args);

//
// ─── Controllers + Swagger ─────────────────────────────────────
//

builder.Services.AddControllers();

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
    options.UseNpgsql(
        builder.Configuration.GetConnectionString("DefaultConnection"),
        npg => npg.MigrationsAssembly(typeof(AppDbContext).Assembly.FullName)
    )
);

//
// ─── Python Agents HTTP Client ─────────────────────────────────
//

builder.Services.Configure<AgentPythonOptions>(
    builder.Configuration.GetSection("AgentPython")
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

    try
    {
        Console.WriteLine("DEBUG: Checking pending migrations...");

        var pending = await context.Database.GetPendingMigrationsAsync();

        Console.WriteLine($"DEBUG: Pending migrations: {string.Join(", ", pending)}");

        // Temporary fallback SQL required by Identity/Profile module
        await context.Database.ExecuteSqlRawAsync("""
            ALTER TABLE utilisateur ADD COLUMN IF NOT EXISTS objectif TEXT;
            ALTER TABLE utilisateur ADD COLUMN IF NOT EXISTS niveau TEXT;
            ALTER TABLE utilisateur ADD COLUMN IF NOT EXISTS secteur TEXT;
            ALTER TABLE utilisateur ADD COLUMN IF NOT EXISTS onboarding_completed BOOLEAN DEFAULT FALSE;
            ALTER TABLE utilisateur ADD COLUMN IF NOT EXISTS onboarding_step INTEGER DEFAULT 0;
            ALTER TABLE utilisateur ADD COLUMN IF NOT EXISTS onboarding_data JSONB;
            ALTER TABLE utilisateur ADD COLUMN IF NOT EXISTS profile_score INTEGER DEFAULT 0;
        """);

        await context.Database.MigrateAsync();

        Console.WriteLine("DEBUG: Startup SQL + migrations applied successfully.");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"DEBUG: Migration/startup SQL error: {ex.Message}");
    }
}

await app.RunAsync();