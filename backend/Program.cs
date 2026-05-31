using NextStep.data;
using NextStep.Shared.Config;
using NextStep.Shared.Http;
using QuestPDF.Infrastructure;
using Hangfire;
using NextStep.Jobs;

AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);

var builder = WebApplication.CreateBuilder(args);

// Register Core Infrastructure
builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.AddDebug();

builder.Services.AddControllers();
builder.Services.ConfigureApiBehavior();
builder.Services.AddSignalR();
builder.Services.AddHttpClient();
builder.Services.AddCorsPolicy();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Database & Auth
builder.Services.AddAppDbContext(builder.Configuration);
builder.Services.AddAppAuthentication(builder.Configuration);

// Application Business Services, Storage & Background Jobs
builder.Services.AddAppBusinessServices(builder.Configuration);
builder.Services.AddAppStorageAndJobs(builder.Configuration);

QuestPDF.Settings.License = LicenseType.Community;

var app = builder.Build();

// Configure Middleware Pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors("Angular");
app.UseAuthentication();
app.UseAuthorization();

// Global Exception Handler
app.Use(async (ctx, next) =>
{
    try { await next(); }
    catch (Exception ex)
    {
        ctx.Response.StatusCode = 500;
        ctx.Response.ContentType = "application/json";
        await ctx.Response.WriteAsync(
            System.Text.Json.JsonSerializer.Serialize(new { error = ex.Message, type = ex.GetType().Name }));
    }
});

app.MapControllers();
app.MapHub<NextStep.SignalR.PipelineHub>("/hubs/pipeline");

// Hangfire Dashboard (Development only) + Recurring Jobs
if (app.Environment.IsDevelopment())
{
    app.UseHangfireDashboard("/hangfire", new DashboardOptions
    {
        Authorization = new[] { new AllowAllHangfireAuthorizationFilter() }
    });
}

RecurringJob.AddOrUpdate<CheckEmailRepliesJob>(
    "check-email-replies",
    job => job.ExecuteAsync(CancellationToken.None),
    Cron.Daily);

RecurringJob.AddOrUpdate<DetectFollowUpNeededJob>(
    "detect-follow-up-needed",
    job => job.ExecuteAsync(CancellationToken.None),
    Cron.Daily);

// Initialize DB schema checks, seed tables, & MinIO storage checks
await app.InitializeDatabaseAsync();

await app.RunAsync();
