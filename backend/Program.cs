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

await app.RunAsync();