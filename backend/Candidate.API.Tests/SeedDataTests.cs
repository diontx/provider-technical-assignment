namespace Candidate.API.Tests;

/// <summary>
/// Guards the committed sample data (Database/Scripts/03_seed_data.sql): it must keep
/// demonstrating every required scenario, and the lookup data must stay complete.
/// </summary>
public sealed class SeedDataTests : IDisposable
{
    private readonly TestDatabase _database = new(seedSampleData: true);

    public void Dispose() => _database.Dispose();

    [Fact]
    public void County_lookup_holds_the_159_georgia_counties_without_duplicates()
    {
        Assert.Equal(159, GeorgiaCounties.Names.Count);
        Assert.Equal(159, GeorgiaCounties.Names.Distinct(StringComparer.OrdinalIgnoreCase).Count());
        Assert.Equal(GeorgiaCounties.Names.OrderBy(n => n, StringComparer.OrdinalIgnoreCase), GeorgiaCounties.Names);

        using var db = _database.CreateContext();
        Assert.Equal(GeorgiaCounties.Names, db.Counties.OrderBy(c => c.CountyId).Select(c => c.Name).ToList());
    }

    [Fact]
    public async Task Sample_data_demonstrates_scenario_1_active_providers_with_active_licenses()
    {
        await using var db = _database.CreateContext();
        var report = await new ReportService(db, _database.Clock).ActiveProvidersWithActiveLicensesAsync();

        Assert.Contains(report.Items, row => row.Licenses.Count > 0);
        Assert.Contains(report.Items, row => row.HasNoValidLicense);
        Assert.All(report.Items, row => Assert.Equal(ProviderStatus.Active, row.Status));
        Assert.All(report.Items.SelectMany(row => row.Licenses), license => Assert.True(license.IsCurrentlyValid));
    }

    [Fact]
    public async Task Sample_data_demonstrates_scenario_2_active_providers_with_expired_licenses()
    {
        await using var db = _database.CreateContext();
        var report = await new ReportService(db, _database.Clock).ActiveProvidersWithExpiredLicensesAsync();

        Assert.Equal(
            ["Columbus Sunshine Daycare", "Decatur Family Child Care", "Savannah Coastal Kids Academy"],
            report.Items.Select(row => row.ProviderName));

        // Includes a license still stored as Active after its date passed, and a provider with a valid one remaining.
        Assert.Contains(report.Items.SelectMany(row => row.Licenses), license => license.LicenseStatus == LicenseStatus.Active);
        Assert.Contains(report.Items, row => row.HasNoValidLicense);
        Assert.Contains(report.Items, row => !row.HasNoValidLicense);
    }

    [Fact]
    public async Task Sample_data_demonstrates_scenario_3_soft_deleted_providers_are_excluded()
    {
        await using var db = _database.CreateContext();

        var listed = await new ProviderService(db, _database.Clock).ListAsync(new ProviderQuery { PageSize = 100 });
        var deleted = await new AuditService(db).DeletedProvidersAsync();

        Assert.Equal(11, listed.TotalCount);
        Assert.Equal(["Albany Tiny Tots", "Rome Hilltop Child Care"], deleted.Select(d => d.ProviderName).Order());
        Assert.DoesNotContain(listed.Items, p => p.ProviderName == "Albany Tiny Tots");
        Assert.Equal(13, await db.Providers.IgnoreQueryFilters().CountAsync());

        // The deleted "Rome Hilltop Child Care" was re-created; only the live one is listed.
        var rome = Assert.Single(listed.Items, p => p.ProviderName == "Rome Hilltop Child Care");
        Assert.Equal(13, rome.ProviderId);
    }

    [Fact]
    public async Task Sample_data_covers_every_provider_and_license_status()
    {
        await using var db = _database.CreateContext();

        Assert.Equal(
            Enum.GetValues<ProviderStatus>(),
            (await db.Providers.Select(p => p.Status).Distinct().ToListAsync()).Order());
        Assert.Equal(
            Enum.GetValues<LicenseStatus>(),
            (await db.Licenses.Select(l => l.LicenseStatus).Distinct().ToListAsync()).Order());
    }

    [Fact]
    public async Task Seed_script_can_be_run_again_without_changing_anything()
    {
        await using var db = _database.CreateContext();

        await db.Database.ExecuteSqlRawAsync(DatabaseInitializer.ReadSeedScript());

        Assert.Equal(13, await db.Providers.IgnoreQueryFilters().CountAsync());
        Assert.Equal(12, await db.Licenses.IgnoreQueryFilters().CountAsync());
    }

    [Fact]
    public async Task Database_views_agree_with_the_api_reports_for_the_sample_data()
    {
        // The views use the machine's date; the sample dates are years away from today in both
        // directions, so the views and the fixed test clock classify every license the same way.
        await using var db = _database.CreateContext();
        var reports = new ReportService(db, _database.Clock);
        var scenario1 = await reports.ActiveProvidersWithActiveLicensesAsync();
        var scenario2 = await reports.ActiveProvidersWithExpiredLicensesAsync();

        var view1 = await db.Database
            .SqlQueryRaw<int>("SELECT COUNT(LicenseId) AS Value FROM vw_ActiveProvidersActiveLicenses").SingleAsync();
        var view1Providers = await db.Database
            .SqlQueryRaw<int>("SELECT COUNT(DISTINCT ProviderId) AS Value FROM vw_ActiveProvidersActiveLicenses").SingleAsync();
        var view2 = await db.Database
            .SqlQueryRaw<int>("SELECT COUNT(*) AS Value FROM vw_ActiveProvidersExpiredLicenses").SingleAsync();
        var view2NoneValid = await db.Database
            .SqlQueryRaw<int>("SELECT COUNT(*) AS Value FROM vw_ActiveProvidersExpiredLicenses WHERE HasNoValidLicense = 1").SingleAsync();

        Assert.Equal(scenario1.Items.Sum(row => row.Licenses.Count), view1);
        Assert.Equal(scenario1.Items.Count, view1Providers);
        Assert.Equal(scenario2.Items.Sum(row => row.Licenses.Count), view2);
        Assert.Equal(scenario2.Items.Where(row => row.HasNoValidLicense).Sum(row => row.Licenses.Count), view2NoneValid);
    }
}
