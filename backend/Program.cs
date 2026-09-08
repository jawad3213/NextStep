using NextStep.data;
using NextStep.Shared.Config;
using NextStep.Shared.Http;
using NextStep.Shared.ErrorHandling;
using Microsoft.Extensions.FileProviders;
using Microsoft.AspNetCore.Diagnostics;
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
builder.Services.AddCorsPolicy(builder.Configuration);
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Global exception handler (.NET 8 IExceptionHandler). UseExceptionHandler()
// (dans la pipeline, plus bas) trouve les handlers enregistrés ici.
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddProblemDetails();

// Database & Auth
builder.Services.AddAppDbContext(builder.Configuration);
builder.Services.AddAppAuthentication(builder.Configuration);

// Application Business Services, Storage & Background Jobs
builder.Services.AddAppBusinessServices(builder.Configuration);
builder.Services.AddAppStorageAndJobs(builder.Configuration, builder.Environment);

QuestPDF.Settings.License = LicenseType.Community;

var app = builder.Build();

// Configure Middleware Pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// Local development storage: serve files written by LocalFileStorageService
// (Storage:Mode = "Local"). Kept out of the pipeline otherwise.
if (string.Equals(app.Configuration["Storage:Mode"], "Local", StringComparison.OrdinalIgnoreCase))
{
    var storageRoot = Path.Combine(app.Environment.ContentRootPath, "storage");
    Directory.CreateDirectory(storageRoot);
    app.UseStaticFiles(new StaticFileOptions
    {
        FileProvider = new PhysicalFileProvider(storageRoot),
        RequestPath = "/uploads"
    });
}

app.UseCors("Angular");

// Global exception handling — MUST be registered before the terminal endpoints
// (MapControllers / MapHub) so exceptions escaping controllers are caught.
app.UseExceptionHandler();
app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/health", () => Results.Ok(new { status = "ok" }));
app.MapGet("/health/live", () => Results.Ok(new { status = "ok" }));

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

var recurringJobManager = app.Services.GetRequiredService<IRecurringJobManager>();

recurringJobManager.AddOrUpdate<CheckEmailRepliesJob>(
    "check-email-replies",
    job => job.ExecuteAsync(CancellationToken.None),
    Cron.Daily);

recurringJobManager.AddOrUpdate<DetectFollowUpNeededJob>(
    "detect-follow-up-needed",
    job => job.ExecuteAsync(CancellationToken.None),
    Cron.Daily);

// Initialize DB schema checks, seed tables, & MinIO storage checks
await app.InitializeDatabaseAsync();

await app.RunAsync();
