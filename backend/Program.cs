using NextStep.Shared.Config;
using NextStep.Shared.Http;
using NextStep.Shared.ErrorHandling;
using NextStep.Shared.Persistence;
using Microsoft.Extensions.FileProviders;
using Microsoft.AspNetCore.Diagnostics;
using QuestPDF.Infrastructure;
using Hangfire;
using NextStep.Modules.Messaging.Application.Jobs;

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
// (in the pipeline below) discovers the handlers registered here.
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddProblemDetails();

// Auth
builder.Services.AddAppAuthentication(builder.Configuration);

// Modules (each with its own DbContext and schema), Storage & Background Jobs
builder.Services.AddAppBusinessServices(builder.Configuration);
builder.Services.AddAppStorageAndJobs(builder.Configuration, builder.Environment);

QuestPDF.Settings.License = LicenseType.Community;

var app = builder.Build();

// Schema ownership: a web container never migrates. Several replicas booting together would run
// the same DDL at the same time and deadlock on each other's locks, so migrations belong to a
// step that runs once, before the rollout. `--migrate` is that step: it applies the schema and
// seeds reference data, then exits without ever serving a request.
if (args.Any(a => string.Equals(a, "--migrate", StringComparison.OrdinalIgnoreCase)))
{
    await app.InitializeDatabaseAsync();
    return;
}

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
app.MapHub<NextStep.Shared.Realtime.PipelineHub>("/hubs/pipeline");

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

// Local convenience: one command brings up an empty database. Outside development the web task
// only checks that the schema matches this build and refuses to start if it does not, which is
// read-only and therefore safe to run on every replica at the same time.
if (app.Environment.IsDevelopment())
    await app.InitializeDatabaseAsync();
else
    await app.VerifySchemaIsUpToDateAsync();

await app.RunAsync();
