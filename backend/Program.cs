using Microsoft.EntityFrameworkCore;
using NextStep.Infrastructure.Data;
using NextStep.Infrastructure.Http;
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

var builder = WebApplication.CreateBuilder(args);

//
// ─── Controllers + Swagger ─────────────────────────────────────
//

builder.Services.AddControllers();

//
// ─── Authentication (Keycloak) ──────────────────────────────────
//

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.Authority = builder.Configuration["Keycloak:Authority"];
        options.Audience = builder.Configuration["Keycloak:Audience"];
        options.RequireHttpsMetadata = builder.Configuration.GetValue<bool>("Keycloak:RequireHttpsMetadata");
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateAudience = true,
            ValidateIssuer = true,
            ValidateLifetime = true
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

    try
    {
        Console.WriteLine("DEBUG: Checking pending migrations...");

        var pending = await context.Database.GetPendingMigrationsAsync();

        if (pending.Any())
        {
            Console.WriteLine($"DEBUG: Applying pending migrations: {string.Join(", ", pending)}");
            await context.Database.MigrateAsync();
        }

        Console.WriteLine("DEBUG: Database is up to date.");

        Console.WriteLine("DEBUG: Startup SQL + migrations applied successfully.");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"DEBUG: Migration/startup SQL error: {ex.Message}");
    }
}

await app.RunAsync();