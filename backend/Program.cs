using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Microsoft.Extensions.Options;
using backend.data;
using Microsoft.EntityFrameworkCore;
using backend.services;
using backend.repository;

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

builder.Services.AddScoped<UserService>();
builder.Services.AddScoped<UserRepository>(); 

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
    });

builder.Services.AddAuthorization();

// Add Swagger Gen
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app =  builder.Build();

// Enable Swagger UI always for now (or wrap in if (app.Environment.IsDevelopment()))
app.UseSwagger();
app.UseSwaggerUI();

app.UseCors("allowAngular");

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

await app.RunAsync();
