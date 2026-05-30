namespace NextStep.Shared.Config;

/// <summary>
/// Configuration options for the automatic follow-up / relance detection policy.
/// Bind from appsettings section "EmailFollowUp".
/// </summary>
public class EmailFollowUpOptions
{
    public const string SectionName = "EmailFollowUp";

    /// <summary>Maximum number of follow-up emails allowed per candidature. Default: 2.</summary>
    public int MaxFollowUps { get; set; } = 2;

    /// <summary>Days to wait after the initial application email before marking a candidature as needing a follow-up.</summary>
    public int DelayDays { get; set; } = 7;

    /// <summary>Minutes override for DelayDays — used in dev/testing to avoid waiting 7 days. Ignored if null or 0.</summary>
    public int? DelayMinutes { get; set; }

    /// <summary>Days to wait between consecutive follow-up emails.</summary>
    public int DelayDaysBetweenFollowUps { get; set; } = 7;

    /// <summary>Minutes override for DelayDaysBetweenFollowUps — dev/testing only. Ignored if null or 0.</summary>
    public int? DelayMinutesBetweenFollowUps { get; set; }

    /// <summary>
    /// Returns the threshold delay after the initial application email.
    /// Uses DelayMinutes if configured and > 0, otherwise DelayDays.
    /// </summary>
    public TimeSpan GetInitialDelay()
    {
        if (DelayMinutes.HasValue && DelayMinutes.Value > 0)
            return TimeSpan.FromMinutes(DelayMinutes.Value);
        return TimeSpan.FromDays(DelayDays);
    }

    /// <summary>
    /// Returns the threshold delay between consecutive follow-up emails.
    /// Uses DelayMinutesBetweenFollowUps if configured and > 0, otherwise DelayDaysBetweenFollowUps.
    /// </summary>
    public TimeSpan GetBetweenFollowUpDelay()
    {
        if (DelayMinutesBetweenFollowUps.HasValue && DelayMinutesBetweenFollowUps.Value > 0)
            return TimeSpan.FromMinutes(DelayMinutesBetweenFollowUps.Value);
        return TimeSpan.FromDays(DelayDaysBetweenFollowUps);
    }
}
