using Hangfire.Dashboard;

namespace NextStep.Shared.Http;

/// <summary>
/// Simple Hangfire filter that allows all requests. 
/// ONLY use this in Development mode.
/// </summary>
public class AllowAllHangfireAuthorizationFilter : IDashboardAuthorizationFilter
{
    public bool Authorize(DashboardContext context)
    {
        // Allow all requests for the dashboard in Dev
        return true;
    }
}
