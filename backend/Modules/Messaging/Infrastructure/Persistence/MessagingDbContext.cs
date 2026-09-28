using Microsoft.EntityFrameworkCore;
using NextStep.Modules.Messaging.Domain;
using NextStep.Shared.Persistence;

namespace NextStep.Modules.Messaging.Infrastructure.Persistence;

/// <summary>Messaging module data: tables of the "messaging" schema only.</summary>
public class MessagingDbContext(DbContextOptions<MessagingDbContext> options) : ModuleDbContext(options)
{
    public const string SchemaName = "messaging";

    public override string Schema => SchemaName;

    public DbSet<EmailDraft> EmailDrafts => Set<EmailDraft>();
    public DbSet<UserEmailConnection> UserEmailConnections => Set<UserEmailConnection>();
    public DbSet<UserOAuthCredential> UserOAuthCredentials => Set<UserOAuthCredential>();
    public DbSet<OAuthState> OAuthStates => Set<OAuthState>();

    protected override void ConfigureModel(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<EmailDraft>(entity =>
        {
            entity.ToTable("email_draft");

            entity.HasKey(e => e.Id);

            entity.Property(e => e.Id)
                .HasColumnName("id_email_draft")
                .HasDefaultValueSql("gen_random_uuid()");

            entity.Property(e => e.CandidatureId)
                .HasColumnName("id_candidature")
                .IsRequired();

            entity.Property(e => e.EmailType)
                .HasColumnName("type_email")
                .HasMaxLength(50)
                .HasDefaultValue("application")
                .IsRequired();

            entity.Property(e => e.RecipientEmail)
                .HasColumnName("recipient_email")
                .HasMaxLength(255);

            entity.Property(e => e.Subject)
                .HasColumnName("objet")
                .HasMaxLength(255)
                .IsRequired();

            entity.Property(e => e.Body)
                .HasColumnName("corps")
                .IsRequired();

            entity.Property(e => e.Language)
                .HasColumnName("langue")
                .HasMaxLength(10)
                .HasDefaultValue("fr");

            entity.Property(e => e.IsApproved)
                .HasColumnName("est_approuve")
                .HasDefaultValue(false);

            entity.Property(e => e.IsSent)
                .HasColumnName("est_envoye")
                .HasDefaultValue(false);

            entity.Property(e => e.CreatedAtUtc)
                .HasColumnName("date_creation")
                .HasDefaultValueSql("now()");

            entity.Property(e => e.UpdatedAtUtc)
                .HasColumnName("date_modification");

            entity.Property(e => e.SentAtUtc)
                .HasColumnName("date_envoi");

            entity.Property(e => e.ApprovedAtUtc)
                .HasColumnName("date_approbation");

            entity.Property(e => e.ErrorMessage)
                .HasColumnName("error_message");

            entity.Property(e => e.ProviderMessageId)
                .HasColumnName("provider_message_id")
                .HasMaxLength(255);

            entity.Property(e => e.ProviderThreadId)
                .HasColumnName("provider_thread_id");

            entity.Property(e => e.SendAttemptCount)
                .HasColumnName("nb_tentatives_envoi")
                .HasDefaultValue(0);

            entity.HasIndex(e => e.CandidatureId)
                .HasDatabaseName("ix_email_draft_candidature");
        });

        modelBuilder.Entity<UserEmailConnection>(entity =>
        {
            entity.ToTable("user_email_connection");

            entity.HasKey(e => e.Id);

            entity.Property(e => e.Id)
                .HasColumnName("id")
                .HasDefaultValueSql("gen_random_uuid()");

            entity.Property(e => e.UserId)
                .HasColumnName("id_utilisateur")
                .IsRequired();

            entity.Property(e => e.Provider)
                .HasColumnName("provider")
                .HasMaxLength(50)
                .IsRequired();

            entity.Property(e => e.EmailAddress)
                .HasColumnName("adresse_email")
                .HasMaxLength(255)
                .IsRequired();

            entity.Property(e => e.AccessTokenEncrypted)
                .HasColumnName("access_token_chiffre")
                .IsRequired();

            entity.Property(e => e.RefreshTokenEncrypted)
                .HasColumnName("refresh_token_chiffre")
                .IsRequired();

            entity.Property(e => e.AccessTokenExpiresAtUtc)
                .HasColumnName("access_token_expire_utc");

            entity.Property(e => e.CreatedAtUtc)
                .HasColumnName("date_creation")
                .HasDefaultValueSql("now()");

            entity.Property(e => e.UpdatedAtUtc)
                .HasColumnName("date_modification");

            entity.HasIndex(e => new { e.UserId, e.Provider })
                .IsUnique();
        });

        modelBuilder.Entity<UserOAuthCredential>(entity =>
        {
            entity.ToTable("user_oauth_credential");

            entity.HasKey(e => e.Id);

            entity.Property(e => e.Id)
                .HasColumnName("id")
                .HasDefaultValueSql("gen_random_uuid()");

            entity.Property(e => e.UserId)
                .HasColumnName("id_utilisateur")
                .IsRequired();

            entity.Property(e => e.Provider)
                .HasColumnName("provider")
                .HasMaxLength(50)
                .IsRequired();

            entity.Property(e => e.ClientIdEncrypted)
                .HasColumnName("client_id_chiffre")
                .IsRequired();

            entity.Property(e => e.ClientSecretEncrypted)
                .HasColumnName("client_secret_chiffre")
                .IsRequired();

            entity.Property(e => e.RedirectUriOverride)
                .HasColumnName("redirect_uri_override")
                .HasMaxLength(1000);

            entity.Property(e => e.CreatedAtUtc)
                .HasColumnName("date_creation")
                .HasDefaultValueSql("now()");

            entity.Property(e => e.UpdatedAtUtc)
                .HasColumnName("date_modification");

            entity.HasIndex(e => new { e.UserId, e.Provider })
                .IsUnique();
        });

        modelBuilder.Entity<OAuthState>(entity =>
        {
            entity.ToTable("oauth_state");

            entity.HasKey(e => e.Id);

            entity.Property(e => e.Id)
                .HasColumnName("id")
                .HasDefaultValueSql("gen_random_uuid()");

            entity.Property(e => e.UserId)
                .HasColumnName("id_utilisateur")
                .IsRequired();

            entity.Property(e => e.Provider)
                .HasColumnName("provider")
                .HasMaxLength(50)
                .IsRequired();

            entity.Property(e => e.StateTokenHash)
                .HasColumnName("state_token_hash")
                .HasMaxLength(64)
                .IsRequired();

            entity.Property(e => e.ExpiresAtUtc)
                .HasColumnName("expire_utc");

            entity.Property(e => e.Used)
                .HasColumnName("utilise")
                .HasDefaultValue(false);

            entity.Property(e => e.CreatedAtUtc)
                .HasColumnName("date_creation")
                .HasDefaultValueSql("now()");

            entity.Property(e => e.UsedAtUtc)
                .HasColumnName("date_utilisation");

            entity.HasIndex(e => e.StateTokenHash)
                .IsUnique();
        });
    }
}
