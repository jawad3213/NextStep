using Microsoft.EntityFrameworkCore;
using NextStep.Modules.Coaching.Domain;
using NextStep.Shared.Persistence;

namespace NextStep.Modules.Coaching.Infrastructure.Persistence;

/// <summary>
/// Coaching module data: tables of the "coaching" schema only.
/// The Python agents write these tables too (interview sessions and questions).
/// </summary>
public class CoachingDbContext(DbContextOptions<CoachingDbContext> options) : ModuleDbContext(options)
{
    public const string SchemaName = "coaching";

    public override string Schema => SchemaName;

    public DbSet<SessionCoaching> SessionCoachings => Set<SessionCoaching>();
    public DbSet<QuestionEntrainement> QuestionEntrainements => Set<QuestionEntrainement>();

    protected override void ConfigureModel(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<SessionCoaching>(entity =>
        {
            // No foreign keys to Profile or Applications: user and candidature are referenced by id only.
            entity.Property(e => e.IdSession).HasDefaultValueSql("gen_random_uuid()");
            entity.Property(e => e.Mode).HasDefaultValue("offer");
            entity.Property(e => e.Language).HasDefaultValue("en");
            entity.Property(e => e.DurationMinutes).HasDefaultValue(20);
            entity.Property(e => e.Status).HasDefaultValue("pending");
            entity.Property(e => e.FocusAreas).HasColumnType("jsonb");
            entity.Property(e => e.FeedbackJson).HasColumnType("jsonb");
            entity.Property(e => e.DateSession).HasDefaultValueSql("now()");

            entity.HasIndex(e => e.IdUtilisateur).HasDatabaseName("idx_session_user");
            entity.HasIndex(e => e.IdCandidature).HasDatabaseName("idx_session_candidature");
        });

        modelBuilder.Entity<QuestionEntrainement>(entity =>
        {
            entity.Property(e => e.IdQuestion).HasDefaultValueSql("gen_random_uuid()");
            entity.HasIndex(e => e.IdSession).HasDatabaseName("idx_question_session");

            entity.HasOne(e => e.Session)
                .WithMany()
                .HasForeignKey(e => e.IdSession)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
