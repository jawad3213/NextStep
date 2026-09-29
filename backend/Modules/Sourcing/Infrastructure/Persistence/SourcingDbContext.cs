using Microsoft.EntityFrameworkCore;
using NextStep.Modules.Sourcing.Domain;
using NextStep.Shared.Persistence;

namespace NextStep.Modules.Sourcing.Infrastructure.Persistence;

/// <summary>Sourcing module data: tables of the "sourcing" schema only.</summary>
public class SourcingDbContext(DbContextOptions<SourcingDbContext> options) : ModuleDbContext(options)
{
    public const string SchemaName = "sourcing";

    public override string Schema => SchemaName;

    public DbSet<SourcedOffer> SourcedOffers => Set<SourcedOffer>();
    public DbSet<ScrapeSession> ScrapeSessions => Set<ScrapeSession>();

    protected override void ConfigureModel(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<SourcedOffer>(entity =>
        {
            entity.ToTable("sourced_offer");

            entity.HasKey(e => e.Id);

            entity.Property(e => e.Id)
                .HasColumnName("id")
                .HasDefaultValueSql("gen_random_uuid()");

            entity.Property(e => e.UserId).HasColumnName("user_id");
            entity.Property(e => e.Provider).HasColumnName("provider").HasMaxLength(40).IsRequired();
            entity.Property(e => e.ProviderJobId).HasColumnName("provider_job_id").HasMaxLength(120);
            entity.Property(e => e.ExternalUrl).HasColumnName("external_url").HasMaxLength(1000);
            entity.Property(e => e.Title).HasColumnName("title").HasMaxLength(300).IsRequired();
            entity.Property(e => e.Company).HasColumnName("company").HasMaxLength(300);
            entity.Property(e => e.Location).HasColumnName("location").HasMaxLength(300);
            entity.Property(e => e.Description).HasColumnName("description");
            entity.Property(e => e.PostedAtText).HasColumnName("posted_at_text").HasMaxLength(120);
            entity.Property(e => e.PostedWindow).HasColumnName("posted_window").HasMaxLength(20);
            entity.Property(e => e.RawContractType).HasColumnName("raw_contract_type").HasMaxLength(120);
            entity.Property(e => e.NormalizedContractType).HasColumnName("normalized_contract_type").HasMaxLength(40);
            entity.Property(e => e.EmploymentType).HasColumnName("employment_type").HasMaxLength(120);
            entity.Property(e => e.SeniorityLevel).HasColumnName("seniority_level").HasMaxLength(120);
            entity.Property(e => e.MatchedItTermsJson).HasColumnName("matched_it_terms_json").HasColumnType("jsonb").HasDefaultValue("[]");
            entity.Property(e => e.SourceQueryJson).HasColumnName("source_query_json").HasColumnType("jsonb").HasDefaultValue("{}");
            entity.Property(e => e.DedupeKey).HasColumnName("dedupe_key").HasMaxLength(500).IsRequired();
            entity.Property(e => e.IsSaved).HasColumnName("is_saved").HasDefaultValue(false);
            entity.Property(e => e.IsShortlisted).HasColumnName("is_shortlisted").HasDefaultValue(false);
            entity.Property(e => e.IsArchived).HasColumnName("is_archived").HasDefaultValue(false);
            entity.Property(e => e.PromotedOfferId).HasColumnName("promoted_offer_id");
            entity.Property(e => e.FirstSeenAtUtc).HasColumnName("first_seen_at_utc").HasDefaultValueSql("now()");
            entity.Property(e => e.LastSeenAtUtc).HasColumnName("last_seen_at_utc").HasDefaultValueSql("now()");
            entity.Property(e => e.ScrapedAtUtc).HasColumnName("scraped_at_utc").HasDefaultValueSql("now()");
            entity.Property(e => e.CreatedAtUtc).HasColumnName("created_at_utc").HasDefaultValueSql("now()");
            entity.Property(e => e.UpdatedAtUtc).HasColumnName("updated_at_utc");

            entity.HasIndex(e => new { e.UserId, e.Provider, e.ProviderJobId }).HasDatabaseName("ix_sourced_offer_user_provider_job");
            entity.HasIndex(e => new { e.UserId, e.DedupeKey }).HasDatabaseName("ix_sourced_offer_user_dedupe");
        });

        modelBuilder.Entity<ScrapeSession>(entity =>
        {
            entity.ToTable("scrape_session");

            entity.HasKey(e => e.Id);

            entity.Property(e => e.Id)
                .HasColumnName("id")
                .HasDefaultValueSql("gen_random_uuid()");

            entity.Property(e => e.UserId).HasColumnName("user_id");
            entity.Property(e => e.Keywords).HasColumnName("keywords").HasMaxLength(300);
            entity.Property(e => e.Location).HasColumnName("location").HasMaxLength(300);
            entity.Property(e => e.ProvidersJson).HasColumnName("providers_json").HasColumnType("jsonb").HasDefaultValue("[]");
            entity.Property(e => e.CountryCode).HasColumnName("country_code").HasMaxLength(20);
            entity.Property(e => e.PostedWindow).HasColumnName("posted_window").HasMaxLength(20);
            entity.Property(e => e.ContractTypesJson).HasColumnName("contract_types_json").HasColumnType("jsonb").HasDefaultValue("[]");
            entity.Property(e => e.Limit).HasColumnName("limit_value").HasDefaultValue(20);
            entity.Property(e => e.ResultCount).HasColumnName("result_count").HasDefaultValue(0);
            entity.Property(e => e.WarningsJson).HasColumnName("warnings_json").HasColumnType("jsonb").HasDefaultValue("[]");
            entity.Property(e => e.ErrorsJson).HasColumnName("errors_json").HasColumnType("jsonb").HasDefaultValue("[]");
            entity.Property(e => e.CreatedAtUtc).HasColumnName("created_at_utc").HasDefaultValueSql("now()");

            entity.HasIndex(e => new { e.UserId, e.CreatedAtUtc }).HasDatabaseName("ix_scrape_session_user_created");
        });
    }
}
