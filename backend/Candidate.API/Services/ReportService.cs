using Candidate.API.Contracts;
using Candidate.API.Data;
using Candidate.API.Domain;
using Microsoft.EntityFrameworkCore;

namespace Candidate.API.Services;

public interface IReportService
{
    /// <summary>Scenario 1: active providers with their currently valid licenses.</summary>
    Task<ProviderLicenseReport> ActiveProvidersWithActiveLicensesAsync(CancellationToken cancellationToken = default);

    /// <summary>Scenario 2: providers that look active but hold at least one expired license.</summary>
    Task<ProviderLicenseReport> ActiveProvidersWithExpiredLicensesAsync(CancellationToken cancellationToken = default);
}

public sealed class ReportService(ProviderDbContext db, IClock clock) : IReportService
{
    public async Task<ProviderLicenseReport> ActiveProvidersWithActiveLicensesAsync(CancellationToken cancellationToken = default)
    {
        var today = clock.Today;

        var providers = await ActiveProviders().ToListAsync(cancellationToken);

        // An active provider with no valid license still appears, with an empty list.
        // Leaving it out would hide exactly the compliance problem a reviewer needs to see.
        var rows = providers
            .Select(p => ToRow(p, today, p.Licenses.Where(l => LicenseRules.IsCurrentlyValid(l, today))))
            .ToList();

        return new ProviderLicenseReport(today, rows);
    }

    public async Task<ProviderLicenseReport> ActiveProvidersWithExpiredLicensesAsync(CancellationToken cancellationToken = default)
    {
        var today = clock.Today;
        var isExpired = LicenseRules.ExpiredOn(today);

        var providers = await ActiveProviders()
            .Where(p => p.Licenses.AsQueryable().Any(isExpired))
            .ToListAsync(cancellationToken);

        var rows = providers
            .Select(p => ToRow(p, today, p.Licenses.Where(l => LicenseRules.IsExpired(l, today))))
            .ToList();

        return new ProviderLicenseReport(today, rows);
    }

    // Soft-deleted providers, and their licenses, are already excluded by the global query filters.
    private IQueryable<Provider> ActiveProviders() =>
        db.Providers
            .AsNoTracking()
            .Where(p => p.Status == ProviderStatus.Active)
            .Include(p => p.County)
            .Include(p => p.Licenses)
            .OrderBy(p => p.ProviderName)
            .ThenBy(p => p.ProviderId);

    private static ProviderLicenseReportRow ToRow(Provider provider, DateOnly today, IEnumerable<License> licenses) => new(
        provider.ProviderId,
        provider.ProviderName,
        provider.County.Name,
        provider.Status,
        HasNoValidLicense: !provider.Licenses.Any(l => LicenseRules.IsCurrentlyValid(l, today)),
        licenses.ToDtos(today));
}
