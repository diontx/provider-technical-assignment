using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;

namespace Candidate.API.Tests.Infrastructure;

/// <summary>
/// Hosts the real API in memory (routing, model binding, validation, JSON, problem details)
/// against a private in-memory SQLite database that is migrated and seeded with the sample data.
/// </summary>
public sealed class ApiFactory : WebApplicationFactory<Program>
{
    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    // A named shared-cache database: every connection the API opens sees the same data,
    // and it disappears when the last connection closes.
    private readonly string _connectionString = $"Data Source=file:api-tests-{Guid.NewGuid():N}?mode=memory&cache=shared";
    private readonly SqliteConnection _keepAlive;

    public ApiFactory()
    {
        _keepAlive = new SqliteConnection(_connectionString);
        _keepAlive.Open();
    }

    public FakeClock Clock { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:Providers", _connectionString);
        builder.UseSetting("Database:InitializeOnStartup", "true");
        builder.UseSetting("Database:SeedSampleData", "true");
        builder.ConfigureTestServices(services => services.AddSingleton<IClock>(Clock));
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing)
        {
            _keepAlive.Dispose();
        }
    }
}
