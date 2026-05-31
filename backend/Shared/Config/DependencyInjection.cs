using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.Extensions.Options;
using Amazon.S3;
using Hangfire;
using Hangfire.PostgreSql;
using NextStep.data;
using NextStep.Shared.Http;
using NextStep.Shared.Storage;
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
using NextStep.Modules.Chatbot;
using NextStep.Jobs;

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

    public static IServiceCollection AddCorsPolicy(this IServiceCollection services)
    {
        services.AddCors(options =>
        {
            options.AddPolicy("Angular", policy =>
                policy.WithOrigins("http://localhost:4200").AllowAnyHeader().AllowAnyMethod().AllowCredentials()
            );
        });
        return services;
    }

    public static IServiceCollection AddAppDbContext(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection");
        services.AddDbContext<AppDbContext>(options => {
            options.UseNpgsql(connectionString);
            options.ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.PendingModelChangesWarning));
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
                    ValidateLifetime = false,
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
                            var userService = context.HttpContext.RequestServices.GetRequiredService<IUserService>();
                            try { await userService.EnsureUserCreatedAsync(principal); } catch { }
                        }
                    }
                };
            });
        return services;
    }

    public static IServiceCollection AddAppBusinessServices(this IServiceCollection services, IConfiguration configuration)
    {
        // Python Agents settings
        services.Configure<AgentPythonOptions>(configuration.GetSection("PythonAgents"));
        services.Configure<SmtpEmailOptions>(configuration.GetSection("Email:Smtp"));
        services.AddHttpClient("SharedAgentClient").AddTypedClient<IAgentHttpClient, AgentHttpClient>();
        
        // Chatbot Module
        services.AddChatbotModule(configuration);

        // Core business services & repositories
        services.AddScoped<IOfferRepository, OfferRepository>();
        services.AddScoped<IOfferService, OfferService>();
        services.AddScoped<IPipelineRunnerService, PipelineRunnerService>();
        services.AddScoped<IPdfGenerationService, PdfGenerationService>();
        services.AddScoped<ICandidatureRepository, CandidatureRepository>();
        services.AddScoped<ICandidatureService, CandidatureService>();
        services.AddScoped<IEmailDraftRepository, EmailDraftRepository>();
        services.AddScoped<IEmailService, EmailService>();
        services.AddScoped<IEmailSenderService, GmailEmailSenderService>();
        services.AddScoped<IEmailConnectionService, EmailConnectionService>();
        services.AddScoped<IUserEmailConnectionRepository, UserEmailConnectionRepository>();
        services.AddScoped<IUserOAuthCredentialRepository, UserOAuthCredentialRepository>();
        services.AddScoped<IOAuthStateRepository, OAuthStateRepository>();
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IUserService, UserService>();
        services.AddScoped<IProfileService, ProfileService>();
        services.AddScoped<IGmailReplyMonitorService, GmailReplyMonitorService>();
        services.AddScoped<IResponseClassificationService, ResponseClassificationService>();
        services.AddScoped<CheckEmailRepliesJob>();
        services.AddScoped<DetectFollowUpNeededJob>();
        services.AddScoped<ICvService, CvService>();
        services.AddScoped<ICvHtmlTemplateRenderer, CvHtmlTemplateRenderer>();
        services.AddScoped<ICvPdfRenderer, CvPdfRenderer>();
        services.AddScoped<ICvTemplateService, CvTemplateService>();
        services.AddScoped<ITemplateThumbnailService, TemplateThumbnailService>();
        services.AddScoped<ISourcedOfferService, SourcedOfferService>();

        // Google OAuth configuration
        services.Configure<GoogleOAuthOptions>(
            configuration.GetSection(GoogleOAuthOptions.SectionName));

        // Email Follow-up configuration
        services.Configure<EmailFollowUpOptions>(
            configuration.GetSection(EmailFollowUpOptions.SectionName));

        // ASP.NET Core Data Protection (encrypts Gmail tokens at rest)
        services.AddDataProtection();

        return services;
    }

    public static IServiceCollection AddAppStorageAndJobs(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection");

        // Hangfire (reply-monitoring recurring job)
        services.AddHangfire(config => config
            .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
            .UseSimpleAssemblyNameTypeSerializer()
            .UseRecommendedSerializerSettings()
            .UsePostgreSqlStorage(opt => opt.UseNpgsqlConnection(connectionString)));
        services.AddHangfireServer();

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

        return services;
    }
}
