using Microsoft.Data.Sqlite;

namespace Candidate.API.Tests.Infrastructure;

/// <summary>Fixed date and time, so license validity does not depend on the day the tests run.</summary>
public sealed class FakeClock : IClock
{
    public DateOnly Today { get; set; } = new(2026, 10, 10);

    public DateTime UtcNow { get; set; } = new(2026, 10, 10, 15, 0, 0, DateTimeKind.Utc);
}

public sealed class TestUser : ICurrentUser
{
    public const string UserName = "test-user";

    public string Name => UserName;
}

/// <summary>
/// A real SQLite database held in memory for one test. It is built by the same migrations as
/// the application, so constraints, partial indexes, triggers, and views all behave as they do
/// in the committed .db file. (The EF Core InMemory provider would enforce none of them.)
/// </summary>
public sealed class TestDatabase : IDisposable
{
    private readonly SqliteConnection _connection = new("Data Source=:memory:");
    private readonly DbContextOptions<ProviderDbContext> _options;

    public TestDatabase(bool seedSampleData = false)
    {
        // The database lives only as long as this connection stays open.
        _connection.Open();

        _options = new DbContextOptionsBuilder<ProviderDbContext>()
            .UseSqlite(_connection)
            .AddInterceptors(new AuditSoftDeleteInterceptor(Clock, new TestUser()))
            .Options;

        using var db = CreateContext();
        db.Database.Migrate();

        if (seedSampleData)
        {
            db.Database.ExecuteSqlRaw(DatabaseInitializer.ReadSeedScript());
        }
    }

    public FakeClock Clock { get; } = new();

    /// <summary>A new context each time, so assertions read from the database and not from tracked entities.</summary>
    public ProviderDbContext CreateContext() => new(_options);

    public static int CountyId(string name)
    {
        var index = GeorgiaCounties.Names.ToList().IndexOf(name);
        return index >= 0 ? index + 1 : throw new ArgumentException($"Unknown county '{name}'.", nameof(name));
    }

    public async Task<Provider> AddProviderAsync(
        string name, string county = "Fulton", ProviderStatus status = ProviderStatus.Active)
    {
        await using var db = CreateContext();
        var provider = new Provider { ProviderName = name, CountyId = CountyId(county), Status = status };
        db.Providers.Add(provider);
        await db.SaveChangesAsync();
        return provider;
    }

    public async Task<License> AddLicenseAsync(
        int providerId, string number, LicenseStatus status, DateOnly expirationDate)
    {
        await using var db = CreateContext();
        var license = new License
        {
            ProviderId = providerId,
            LicenseNumber = number,
            LicenseStatus = status,
            ExpirationDate = expirationDate
        };
        db.Licenses.Add(license);
        await db.SaveChangesAsync();
        return license;
    }

    public async Task SoftDeleteProviderAsync(int providerId)
    {
        await using var db = CreateContext();
        var result = await new ProviderService(db, Clock).DeleteAsync(providerId);
        Assert.True(result.IsSuccess);
    }

    public void Dispose() => _connection.Dispose();
}
