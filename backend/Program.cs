using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Microsoft.Extensions.Options;
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


// Repositories
builder.Services.AddScoped<ICandidatureRepository, CandidatureRepository>();
builder.Services.AddScoped<IEmailDraftRepository, EmailDraftRepository>();

// Services
builder.Services.AddScoped<ICandidatureService, CandidatureService>();
builder.Services.AddScoped<IEmailService, EmailService>();

// HTTP client for Python agents
builder.Services.AddHttpClient<IAgentHttpClient, AgentHttpClient>(client =>
{
    client.BaseAddress = new Uri(builder.Configuration["Agents:BaseUrl"]!);
    client.Timeout = TimeSpan.FromSeconds(20);
});

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
            NameClaimType = "preferred_username",
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

await app.RunAsync();
