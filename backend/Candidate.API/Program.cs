using System.Text.Json.Serialization;
using Candidate.API.Data;
using Candidate.API.Services;
using Microsoft.AspNetCore.Mvc.ModelBinding.Metadata;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services
    .AddControllers(options =>
    {
        // Validation errors are keyed by JSON property name ("providerName"), matching what the UI sends.
        options.ModelMetadataDetailsProviders.Add(new SystemTextJsonValidationMetadataProvider());
    })
    .AddJsonOptions(options => options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));

// Unhandled exceptions and bare status codes are returned as RFC 7807 problem details.
// No stack trace is included in the response in any environment; details go to the log.
builder.Services.AddProblemDetails();

// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<IClock, EasternClock>();
builder.Services.AddSingleton<ICurrentUser, AnonymousCurrentUser>();
builder.Services.AddSingleton<AuditSoftDeleteInterceptor>();

builder.Services.AddDbContext<ProviderDbContext>((services, options) =>
{
    // Read here rather than above so configuration overrides (tests, environment variables) apply.
    var configuration = services.GetRequiredService<IConfiguration>();
    var environment = services.GetRequiredService<IHostEnvironment>();
    var connectionString = configuration.GetConnectionString("Providers")
        ?? throw new InvalidOperationException("Connection string 'Providers' is not configured.");

    options
        .UseSqlite(DatabaseInitializer.ResolveConnectionString(connectionString, environment.ContentRootPath))
        .AddInterceptors(services.GetRequiredService<AuditSoftDeleteInterceptor>());
});

builder.Services.AddScoped<IProviderService, ProviderService>();
builder.Services.AddScoped<IReportService, ReportService>();
builder.Services.AddScoped<IAuditService, AuditService>();
builder.Services.AddScoped<ILookupService, LookupService>();
builder.Services.AddScoped<IDashboardService, DashboardService>();

var app = builder.Build();

// "dotnet run -- --init-db" creates or upgrades the database, loads the sample data, and exits.
if (args.Contains("--init-db"))
{
    await DatabaseInitializer.InitializeAsync(app.Services, seedSampleData: true);
    SqliteConnection.ClearAllPools();
    return;
}

// A development convenience. Production would apply migrations from the release pipeline.
if (app.Configuration.GetValue<bool>("Database:InitializeOnStartup"))
{
    await DatabaseInitializer.InitializeAsync(app.Services, app.Configuration.GetValue<bool>("Database:SeedSampleData"));
}

// Configure the HTTP request pipeline.
app.UseExceptionHandler();
app.UseStatusCodePages();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}
else
{
    // Skipped in Development: the Vite dev server proxies /api to the http endpoint, and a
    // redirect to https would send the browser to a different origin.
    app.UseHttpsRedirection();
}

app.UseAuthorization();

app.MapControllers();

app.Run();

/// <summary>Exposed so the test project can host the API with WebApplicationFactory.</summary>
public partial class Program;
