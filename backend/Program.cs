// ============================================================
// Program.cs — Point d'entrée ASP.NET Core
// ============================================================
using Microsoft.EntityFrameworkCore;
using NextStep.Infrastructure.Data;
using NextStep.Infrastructure.Http;
using NextStep.Modules.Offer.Repositories;
using NextStep.Modules.Offer.Services;

var builder = WebApplication.CreateBuilder(args);

// ─── Services ───
builder.Services.AddControllers();
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

// ─── Pipeline HTTP ───
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.UseSwagger();
    app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "NextStep API v1"));
}

app.UseCors("Angular");
app.UseAuthorization();
app.MapControllers();

// ─── Health check rapide ───
app.MapGet("/health", () => Results.Ok(new { status = "ok", service = "nextstep-backend" }));

app.Run();
