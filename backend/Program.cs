var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new() { Title = "NextStep API", Version = "v1" });
});
builder.Services.AddOpenApi();

// ─── Base de données (PostgreSQL + EF Core) ───
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(
        builder.Configuration.GetConnectionString("DefaultConnection"),
        npg => npg.MigrationsAssembly(typeof(AppDbContext).Assembly.FullName)
    )
);

// ─── HTTP Client vers les agents Python ───
builder.Services.Configure<AgentPythonOptions>(
    builder.Configuration.GetSection("AgentPython")
);
builder.Services.AddHttpClient<AgentHttpClient>();

// ─── Module Offer (M2) ───
builder.Services.AddScoped<IOfferRepository, OfferRepository>();
builder.Services.AddScoped<IOfferService, OfferService>();

// ─── CORS (Angular dev) ───
builder.Services.AddCors(options =>
{
    options.AddPolicy("Angular", policy =>
        policy.WithOrigins("http://localhost:4200")
              .AllowAnyHeader()
              .AllowAnyMethod()
    );
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseAuthorization();
app.MapControllers();

// ─── Health check rapide ───
app.MapGet("/health", () => Results.Ok(new { status = "ok", service = "nextstep-backend" }));

// Auto-migration on start
using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    try {
        Console.WriteLine("DEBUG: Checking for pending migrations...");
        var pending = await context.Database.GetPendingMigrationsAsync();
        Console.WriteLine($"DEBUG: Pending migrations: {string.Join(", ", pending)}");
        // Fallback: Ensure columns exist manually
        await context.Database.ExecuteSqlRawAsync("ALTER TABLE utilisateur ADD COLUMN IF NOT EXISTS objectif TEXT;");
        await context.Database.ExecuteSqlRawAsync("ALTER TABLE utilisateur ADD COLUMN IF NOT EXISTS niveau TEXT;");
        await context.Database.ExecuteSqlRawAsync("ALTER TABLE utilisateur ADD COLUMN IF NOT EXISTS secteur TEXT;");
        await context.Database.ExecuteSqlRawAsync("ALTER TABLE utilisateur ADD COLUMN IF NOT EXISTS onboarding_completed BOOLEAN DEFAULT FALSE;");
        await context.Database.ExecuteSqlRawAsync("ALTER TABLE utilisateur ADD COLUMN IF NOT EXISTS onboarding_step INTEGER DEFAULT 0;");
        await context.Database.ExecuteSqlRawAsync("ALTER TABLE utilisateur ADD COLUMN IF NOT EXISTS onboarding_data JSONB;");
        await context.Database.ExecuteSqlRawAsync("ALTER TABLE utilisateur ADD COLUMN IF NOT EXISTS profile_score INTEGER DEFAULT 0;");
        
        await context.Database.MigrateAsync();
        Console.WriteLine("DEBUG: Migrations/SQL applied successfully!");
    } catch (Exception ex) {
        Console.WriteLine($"DEBUG: Migration error: {ex.Message}");
    }
}

await app.RunAsync();