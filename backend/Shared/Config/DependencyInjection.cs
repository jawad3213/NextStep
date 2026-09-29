using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.Extensions.Options;
using Amazon.S3;
using Hangfire;
using Hangfire.PostgreSql;
using NextStep.Shared.Events;
using NextStep.Shared.Http;
using NextStep.Shared.Storage;
using NextStep.Modules.Applications;
using NextStep.Modules.Coaching;
using NextStep.Modules.CvDocuments;
using NextStep.Modules.Messaging;
using NextStep.Modules.Profile;
using NextStep.Modules.Profile.Contracts;
using NextStep.Modules.Sourcing;

namespace NextStep.Shared.Config;

public static class DependencyInjection
{
    public static IServiceCollection ConfigureApiBehavior(this IServiceCollection services)
    {
        services.Configure<ApiBehaviorOptions>(options =>
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
        return services;
    }

    public static IServiceCollection AddCorsPolicy(this IServiceCollection services, IConfiguration configuration)
    {
        var allowedOrigins = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "http://localhost:4200"
        };

        var configuredFrontendBaseUrl = configuration["App:FrontendBaseUrl"];
        if (Uri.TryCreate(configuredFrontendBaseUrl, UriKind.Absolute, out var frontendBaseUri))
        {
            allowedOrigins.Add(frontendBaseUri.GetLeftPart(UriPartial.Authority));
        }

        services.AddCors(options =>
        {
            options.AddPolicy("Angular", policy =>
                policy.SetIsOriginAllowed(_ => true)
                      .AllowAnyHeader()
                      .AllowAnyMethod()
                      .AllowCredentials()
            );
        });
        return services;
    }

    public static IServiceCollection AddAppAuthentication(this IServiceCollection services, IConfiguration configuration)
    {
        Microsoft.IdentityModel.Logging.IdentityModelEventSource.ShowPII = true;

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.Authority = configuration["Keycloak:Authority"];
                options.Audience = configuration["Keycloak:Audience"];
                options.RequireHttpsMetadata = false;
                options.MetadataAddress = $"{configuration["Keycloak:Authority"]}/.well-known/openid-configuration";
                options.TokenValidationParameters = new TokenValidationParameters 
                { 
                    ValidateAudience = false, 
                    ValidateIssuer = false, 
                    ValidateLifetime = true,
                    NameClaimType = "email" 
                };
                options.MapInboundClaims = false;
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
                            var profile = context.HttpContext.RequestServices.GetRequiredService<IProfileApi>();
                            try { await profile.EnsureUserIdAsync(principal); } catch { }
                        }
                    }
                };
            });
        return services;
    }

    public static IServiceCollection AddAppBusinessServices(this IServiceCollection services, IConfiguration configuration)
    {
        // Python Agents settings
        services.Configure<AgentPythonOptions>(options =>
        {
            var url = configuration["PythonAgents:Url"]
                   ?? configuration["AgentsService:BaseUrl"]
                   ?? "http://localhost:8000";
            options.Url = url;
        });
        services.AddTransient<AgentApiKeyHandler>();
        services.AddHttpClient("SharedAgentClient")
            .AddHttpMessageHandler<AgentApiKeyHandler>()
            .AddTypedClient<IAgentHttpClient, AgentHttpClient>();

        // Modules communicate through Contracts and integration events only
        services.AddScoped<IEventPublisher, InProcessEventPublisher>();

        // Each module registers its own DbContext (own schema), services and event handlers
        services.AddProfileModule(configuration);
        services.AddApplicationsModule(configuration);
        services.AddCvDocumentsModule(configuration);
        services.AddMessagingModule(configuration);
        services.AddCoachingModule(configuration);
        services.AddSourcingModule(configuration);

        // ASP.NET Core Data Protection (encrypts Gmail tokens at rest)
        services.AddDataProtection();

        return services;
    }

    public static IServiceCollection AddAppStorageAndJobs(this IServiceCollection services, IConfiguration configuration, IWebHostEnvironment environment)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection");

        // Hangfire (reply-monitoring recurring job)
        services.AddHangfire(config => config
            .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
            .UseSimpleAssemblyNameTypeSerializer()
            .UseRecommendedSerializerSettings()
            .UsePostgreSqlStorage(opt => opt.UseNpgsqlConnection(connectionString)));
        services.AddHangfireServer();

        var storageMode = configuration["Storage:Mode"] ?? "Minio";

        if (string.Equals(storageMode, "Local", StringComparison.OrdinalIgnoreCase))
        {
            // Local development only: persist files on disk and serve them via
            // the backend static-file middleware (see Program.cs /uploads).
            services.Configure<LocalStorageOptions>(options =>
            {
                options.RootPath = Path.Combine(environment.ContentRootPath, "storage");
                options.PublicBaseUrl = configuration["App:PublicBaseUrl"] ?? "http://localhost:5000";
            });
            services.AddSingleton<IStorageService, LocalFileStorageService>();
        }
        else
        {
            // MinIO / S3 Storage
            services.Configure<MinioOptions>(configuration.GetSection("Minio"));
            services.AddSingleton<IAmazonS3>(sp =>
            {
                var opts = sp.GetRequiredService<IOptions<MinioOptions>>().Value;
                var config = new AmazonS3Config
                {
                    ServiceURL = opts.Endpoint,
                    ForcePathStyle = true,   // Required for MinIO
                };
                return new AmazonS3Client(opts.AccessKey, opts.SecretKey, config);
            });
            services.AddSingleton<IStorageService, MinioStorageService>();
        }

        return services;
    }
}
