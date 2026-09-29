using NextStep.Modules.Applications.Contracts;
using NextStep.Modules.Messaging.Infrastructure.Persistence;
using NextStep.Modules.Messaging.Application.EventHandlers;
using NextStep.Modules.Messaging.Application.Jobs;
using NextStep.Modules.Messaging.Infrastructure.Repositories;
using NextStep.Modules.Messaging.Application.Services;
using NextStep.Modules.Messaging.Infrastructure.Gmail;
using NextStep.Shared.Config;
using NextStep.Shared.Events;
using NextStep.Shared.Persistence;

namespace NextStep.Modules.Messaging;

/// <summary>Messaging module: email drafts, Gmail OAuth and reply monitoring (schema "messaging").</summary>
public static class MessagingModule
{
    public static IServiceCollection AddMessagingModule(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddModuleDbContext<MessagingDbContext>(configuration, MessagingDbContext.SchemaName);

        services.Configure<SmtpEmailOptions>(configuration.GetSection("Email:Smtp"));
        services.Configure<GoogleOAuthOptions>(configuration.GetSection(GoogleOAuthOptions.SectionName));
        services.Configure<EmailFollowUpOptions>(configuration.GetSection(EmailFollowUpOptions.SectionName));

        services.AddScoped<IEmailDraftRepository, EmailDraftRepository>();
        services.AddScoped<EmailContextBuilder>();
        services.AddScoped<EmailDraftOwnership>();
        services.AddScoped<IEmailDraftService, EmailDraftService>();
        services.AddScoped<IEmailSendingService, EmailSendingService>();
        services.AddScoped<IFollowUpEmailService, FollowUpEmailService>();
        services.AddScoped<IReplyEmailService, ReplyEmailService>();
        services.AddScoped<IGmailTokenProvider, GmailTokenProvider>();
        services.AddScoped<IEmailSenderService, GmailEmailSenderService>();
        services.AddScoped<IEmailConnectionService, EmailConnectionService>();
        services.AddScoped<IUserEmailConnectionRepository, UserEmailConnectionRepository>();
        services.AddScoped<IUserOAuthCredentialRepository, UserOAuthCredentialRepository>();
        services.AddScoped<IOAuthStateRepository, OAuthStateRepository>();
        services.AddScoped<IGmailReplyMonitorService, GmailReplyMonitorService>();
        services.AddScoped<IResponseClassificationService, ResponseClassificationService>();

        // Background jobs (scheduled in Program.cs)
        services.AddScoped<CheckEmailRepliesJob>();
        services.AddScoped<DetectFollowUpNeededJob>();

        // Reactions to other modules' events
        services.AddScoped<IIntegrationEventHandler<CandidaturesDeleted>, DeleteDraftsOnCandidaturesDeleted>();
        return services;
    }
}
