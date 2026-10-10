using Candidate.API.Contracts;
using Candidate.API.Data;
using Candidate.API.Domain;
using Microsoft.EntityFrameworkCore;

namespace Candidate.API.Services;

public interface IDashboardService
{
    Task<DashboardSummary> SummaryAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Counts for the read-only dashboard. Everything is computed here, with the same license
/// rules as the rest of the API, so the UI only draws what it is given.
/// </summary>
public sealed class DashboardService(ProviderDbContext db, IClock clock) : IDashboardService
{
    public const int ExpiringWindowDays = 90;
    private const int TopCounties = 8;

    public async Task<DashboardSummary> SummaryAsync(CancellationToken cancellationToken = default)
    {
        var today = clock.Today;
        var windowEnd = today.AddDays(ExpiringWindowDays);
        var isValid = LicenseRules.ValidOn(today);
        var isExpired = LicenseRules.ExpiredOn(today);

        var providers = db.Providers.AsNoTracking();
        var licenses = db.Licenses.AsNoTracking();

        var statusCounts = await providers
            .GroupBy(p => p.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);

        var countyCounts = await providers
            .GroupBy(p => p.County.Name)
            .Select(g => new { County = g.Key, Count = g.Count() })
            .OrderByDescending(g => g.Count)
            .ThenBy(g => g.County)
            .Take(TopCounties)
            .ToListAsync(cancellationToken);

        var totalLicenses = await licenses.CountAsync(cancellationToken);
        var validLicenses = await licenses.CountAsync(isValid, cancellationToken);
        var expiredLicenses = await licenses.CountAsync(isExpired, cancellationToken);
        var suspendedLicenses = await licenses.CountAsync(
            l => l.LicenseStatus == LicenseStatus.Suspended && l.ExpirationDate >= today, cancellationToken);

        var activeProviders = providers.Where(p => p.Status == ProviderStatus.Active);
        var withoutValid = await activeProviders.CountAsync(p => !p.Licenses.AsQueryable().Any(isValid), cancellationToken);
        var withExpired = await activeProviders.CountAsync(p => p.Licenses.AsQueryable().Any(isExpired), cancellationToken);

        var expiring = await licenses
            .Where(isValid)
            .Where(l => l.ExpirationDate <= windowEnd)
            .OrderBy(l => l.ExpirationDate)
            .Select(l => new { l.ProviderId, l.Provider.ProviderName, l.LicenseNumber, l.ExpirationDate })
            .ToListAsync(cancellationToken);

        return new DashboardSummary(
            today,
            statusCounts.Sum(s => s.Count),
            totalLicenses,
            withoutValid,
            withExpired,
            // Every status is listed, including those with a zero count, in declaration order.
            Enum.GetValues<ProviderStatus>()
                .Select(s => new NamedCount(s.ToString(), statusCounts.FirstOrDefault(c => c.Status == s)?.Count ?? 0))
                .ToList(),
            [
                new NamedCount("Valid", validLicenses),
                new NamedCount("Expired", expiredLicenses),
                new NamedCount("Suspended", suspendedLicenses),
                // Not expired by date, but stored with a status other than Active or Suspended.
                new NamedCount("Other not valid", totalLicenses - validLicenses - expiredLicenses - suspendedLicenses)
            ],
            countyCounts.Select(c => new NamedCount(c.County, c.Count)).ToList(),
            ExpiringWindowDays,
            expiring
                .Select(l => new ExpiringLicenseDto(
                    l.ProviderId, l.ProviderName, l.LicenseNumber, l.ExpirationDate, l.ExpirationDate.DayNumber - today.DayNumber))
                .ToList());
    }
}
