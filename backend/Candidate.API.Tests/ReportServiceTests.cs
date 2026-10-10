namespace Candidate.API.Tests;

/// <summary>The required data scenarios, evaluated against a fixed "today" of 2026-10-10.</summary>
public sealed class ReportServiceTests : IDisposable
{
    private readonly TestDatabase _database = new();
    private DateOnly Today => _database.Clock.Today;

    public void Dispose() => _database.Dispose();

    private async Task<ProviderLicenseReport> Scenario1Async()
    {
        await using var db = _database.CreateContext();
        return await new ReportService(db, _database.Clock).ActiveProvidersWithActiveLicensesAsync();
    }

    private async Task<ProviderLicenseReport> Scenario2Async()
    {
        await using var db = _database.CreateContext();
        return await new ReportService(db, _database.Clock).ActiveProvidersWithExpiredLicensesAsync();
    }

    private async Task<Provider> ProviderWithLicenseAsync(
        string name, ProviderStatus providerStatus, LicenseStatus licenseStatus, DateOnly expirationDate, string county = "Fulton")
    {
        var provider = await _database.AddProviderAsync(name, county, providerStatus);
        await _database.AddLicenseAsync(provider.ProviderId, $"{name}-LIC", licenseStatus, expirationDate);
        return provider;
    }

    // Scenario 1: active providers and their active licenses

    [Fact]
    public async Task Scenario1_lists_only_active_providers_with_only_their_valid_licenses()
    {
        var active = await _database.AddProviderAsync("Active Provider");
        await _database.AddLicenseAsync(active.ProviderId, "VALID", LicenseStatus.Active, Today.AddYears(1));
        await _database.AddLicenseAsync(active.ProviderId, "EXPIRED", LicenseStatus.Active, Today.AddDays(-1));
        await _database.AddLicenseAsync(active.ProviderId, "SUSPENDED", LicenseStatus.Suspended, Today.AddYears(1));
        await ProviderWithLicenseAsync("Inactive Provider", ProviderStatus.Inactive, LicenseStatus.Active, Today.AddYears(1), "DeKalb");
        await ProviderWithLicenseAsync("Pending Provider", ProviderStatus.Pending, LicenseStatus.Active, Today.AddYears(1), "Cobb");

        var report = await Scenario1Async();

        Assert.Equal(Today, report.AsOfDate);
        var row = Assert.Single(report.Items);
        Assert.Equal("Active Provider", row.ProviderName);
        Assert.False(row.HasNoValidLicense);
        Assert.Equal("VALID", Assert.Single(row.Licenses).LicenseNumber);
    }

    [Fact]
    public async Task Scenario1_keeps_an_active_provider_that_has_no_valid_license()
    {
        await _database.AddProviderAsync("No Licenses");

        var row = Assert.Single((await Scenario1Async()).Items);

        Assert.Empty(row.Licenses);
        Assert.True(row.HasNoValidLicense);
    }

    [Fact]
    public async Task License_is_valid_on_its_expiration_date_and_expired_the_day_after()
    {
        await ProviderWithLicenseAsync("Boundary", ProviderStatus.Active, LicenseStatus.Active, Today);

        Assert.Single(Assert.Single((await Scenario1Async()).Items).Licenses);
        Assert.Empty((await Scenario2Async()).Items);

        _database.Clock.Today = Today.AddDays(1);

        Assert.Empty(Assert.Single((await Scenario1Async()).Items).Licenses);
        Assert.Single(Assert.Single((await Scenario2Async()).Items).Licenses);
    }

    // Scenario 2: providers appearing active but having expired licenses

    [Fact]
    public async Task Scenario2_finds_a_license_whose_stored_status_is_still_active_after_its_date_has_passed()
    {
        await ProviderWithLicenseAsync("Status Drift", ProviderStatus.Active, LicenseStatus.Active, Today.AddMonths(-6));

        var row = Assert.Single((await Scenario2Async()).Items);

        Assert.Equal("Status Drift", row.ProviderName);
        Assert.True(row.HasNoValidLicense);
        var license = Assert.Single(row.Licenses);
        Assert.Equal(LicenseStatus.Active, license.LicenseStatus);
        Assert.True(license.IsExpired);
        Assert.Equal(LicenseStatus.Expired, license.EffectiveStatus);
    }

    [Fact]
    public async Task Scenario2_returns_only_the_expired_licenses_and_flags_whether_a_valid_one_remains()
    {
        var provider = await _database.AddProviderAsync("One Of Each");
        await _database.AddLicenseAsync(provider.ProviderId, "OLD", LicenseStatus.Expired, Today.AddYears(-2));
        await _database.AddLicenseAsync(provider.ProviderId, "CURRENT", LicenseStatus.Active, Today.AddYears(2));

        var row = Assert.Single((await Scenario2Async()).Items);

        Assert.False(row.HasNoValidLicense);
        Assert.Equal("OLD", Assert.Single(row.Licenses).LicenseNumber);
    }

    [Fact]
    public async Task Scenario2_excludes_providers_that_are_not_active_or_have_nothing_expired()
    {
        await ProviderWithLicenseAsync("Inactive Expired", ProviderStatus.Inactive, LicenseStatus.Expired, Today.AddYears(-1));
        await ProviderWithLicenseAsync("Active Valid", ProviderStatus.Active, LicenseStatus.Active, Today.AddYears(1), "DeKalb");
        await ProviderWithLicenseAsync("Active Suspended", ProviderStatus.Active, LicenseStatus.Suspended, Today.AddYears(1), "Cobb");
        await _database.AddProviderAsync("Active No Licenses", "Clarke");

        Assert.Empty((await Scenario2Async()).Items);
    }

    // Scenario 3: exclusion of soft-deleted providers

    [Fact]
    public async Task Soft_deleted_providers_appear_in_neither_report()
    {
        var valid = await ProviderWithLicenseAsync("Deleted Valid", ProviderStatus.Active, LicenseStatus.Active, Today.AddYears(1));
        var expired = await ProviderWithLicenseAsync("Deleted Expired", ProviderStatus.Active, LicenseStatus.Active, Today.AddYears(-1), "DeKalb");
        await _database.SoftDeleteProviderAsync(valid.ProviderId);
        await _database.SoftDeleteProviderAsync(expired.ProviderId);

        Assert.Empty((await Scenario1Async()).Items);
        Assert.Empty((await Scenario2Async()).Items);
    }

    [Fact]
    public async Task Soft_deleted_licenses_are_ignored_by_both_reports()
    {
        var provider = await _database.AddProviderAsync("Deleted License");
        var license = await _database.AddLicenseAsync(provider.ProviderId, "REMOVED", LicenseStatus.Active, Today.AddYears(-1));
        await using (var db = _database.CreateContext())
        {
            db.Licenses.Remove(await db.Licenses.SingleAsync(l => l.LicenseId == license.LicenseId));
            await db.SaveChangesAsync();
        }

        Assert.Empty(Assert.Single((await Scenario1Async()).Items).Licenses);
        Assert.Empty((await Scenario2Async()).Items);
    }

    // Dashboard uses the same rules

    [Fact]
    public async Task Dashboard_counts_follow_the_same_license_rules()
    {
        var provider = await _database.AddProviderAsync("Dashboard Provider");
        await _database.AddLicenseAsync(provider.ProviderId, "VALID", LicenseStatus.Active, Today.AddYears(1));
        await _database.AddLicenseAsync(provider.ProviderId, "SOON", LicenseStatus.Active, Today.AddDays(30));
        await _database.AddLicenseAsync(provider.ProviderId, "DRIFT", LicenseStatus.Active, Today.AddDays(-1));
        await _database.AddLicenseAsync(provider.ProviderId, "SUSPENDED", LicenseStatus.Suspended, Today.AddYears(1));
        await _database.AddProviderAsync("No Licenses", "DeKalb");
        await _database.AddProviderAsync("Inactive", "Cobb", ProviderStatus.Inactive);
        var deleted = await _database.AddProviderAsync("Deleted", "Clarke");
        await _database.SoftDeleteProviderAsync(deleted.ProviderId);

        await using var db = _database.CreateContext();
        var summary = await new DashboardService(db, _database.Clock).SummaryAsync();

        Assert.Equal(3, summary.TotalProviders);
        Assert.Equal(4, summary.TotalLicenses);
        Assert.Equal(1, summary.ActiveProvidersWithoutValidLicense);
        Assert.Equal(1, summary.ActiveProvidersWithExpiredLicense);
        Assert.Equal(
            [new NamedCount("Active", 2), new NamedCount("Inactive", 1), new NamedCount("Pending", 0)],
            summary.ProvidersByStatus);
        Assert.Equal(2, summary.LicensesByEffectiveStatus.Single(c => c.Name == "Valid").Count);
        Assert.Equal(1, summary.LicensesByEffectiveStatus.Single(c => c.Name == "Expired").Count);
        Assert.Equal(1, summary.LicensesByEffectiveStatus.Single(c => c.Name == "Suspended").Count);

        var expiring = Assert.Single(summary.ExpiringLicenses);
        Assert.Equal("SOON", expiring.LicenseNumber);
        Assert.Equal(30, expiring.DaysRemaining);
    }
}
