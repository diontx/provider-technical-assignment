using System.Reflection;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Candidate.API.Data;

/// <summary>
/// Applies migrations and loads the sample data. Used at startup in Development and by
/// <c>dotnet run -- --init-db</c>. A production deployment would run migrations from the
/// release pipeline instead, and would not load sample data.
/// </summary>
public static class DatabaseInitializer
{
    public const string SeedScriptResource = "Candidate.API.Database.Scripts.03_seed_data.sql";

    public static async Task InitializeAsync(IServiceProvider services, bool seedSampleData, CancellationToken cancellationToken = default)
    {
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ProviderDbContext>();
        var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger(typeof(DatabaseInitializer));

        await db.Database.MigrateAsync(cancellationToken);

        if (!seedSampleData)
        {
            return;
        }

        // Deleted providers count too: a database that has ever held data is left alone.
        if (await db.Providers.IgnoreQueryFilters().AnyAsync(cancellationToken))
        {
            logger.LogInformation("Sample data not loaded: the Provider table already has rows.");
            return;
        }

        await db.Database.ExecuteSqlRawAsync(ReadSeedScript(), cancellationToken);
        logger.LogInformation("Sample data loaded from {Script}.", SeedScriptResource);
    }

    /// <summary>
    /// The seed is the same SQL script that is committed under Database/Scripts, embedded in
    /// the assembly, so the application and a reviewer running the script by hand load identical data.
    /// </summary>
    public static string ReadSeedScript()
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(SeedScriptResource)
            ?? throw new InvalidOperationException($"Embedded seed script '{SeedScriptResource}' was not found.");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    /// <summary>
    /// Resolves a relative SQLite file path against the content root, so the same database
    /// file is used from Visual Studio, <c>dotnet run</c>, and <c>dotnet ef</c>.
    /// </summary>
    public static string ResolveConnectionString(string connectionString, string contentRootPath)
    {
        var builder = new SqliteConnectionStringBuilder(connectionString);
        var dataSource = builder.DataSource;

        var isFile = !string.IsNullOrWhiteSpace(dataSource)
            && dataSource != ":memory:"
            && !dataSource.StartsWith("file:", StringComparison.OrdinalIgnoreCase);

        if (!isFile)
        {
            return connectionString;
        }

        if (!Path.IsPathRooted(dataSource))
        {
            builder.DataSource = Path.GetFullPath(Path.Combine(contentRootPath, dataSource));
        }

        Directory.CreateDirectory(Path.GetDirectoryName(builder.DataSource)!);

        return builder.ToString();
    }
}
