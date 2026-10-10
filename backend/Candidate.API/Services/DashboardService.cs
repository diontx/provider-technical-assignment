using Candidate.API.Contracts;
using Candidate.API.Data;
using Candidate.API.Domain;
using Microsoft.EntityFrameworkCore;

namespace Candidate.API.Services;

public interface IDashboardService
{
    Task<DashboardSummary> SummaryAsync(CancellationToken cancellationToken = default);

    /// <summary>The providers behind a dashboard number.</summary>
    Task<IReadOnlyList<ProviderListItem>> ProvidersAsync(DashboardProviderQuery query, CancellationToken cancellationToken = default);

    /// <summary>The licenses behind a dashboard number. Null lists every license.</summary>
    Task<IReadOnlyList<DashboardLicenseRow>> LicensesAsync(LicenseValidity? validity, CancellationToken cancellationToken = default);
}

/// <summary>
/// Counts for the read-only dashboard. Everything is computed here, with the same license
/// rules as the rest of the API, so the UI only draws what it is given.
/// </summary>
public sealed class DashboardService(ProviderDbContext db, IClock clock) : IDashboardService
{
    public const int ExpiringWindowDays = 90;
    public const int TopProvidersByLicenses = 10;

    /// <summary>Upper bound on a record list. The dashboard is an overview, not an export.</summary>
    public const int MaxRecords = 500;

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
        var suspendedLicenses = await InBucket(licenses, LicenseValidity.Suspended, today).CountAsync(cancellationToken);

        var withoutValid = await WithGap(providers, LicenseGap.NoValidLicense, today).CountAsync(cancellationToken);
        var withExpired = await WithGap(providers, LicenseGap.ExpiredLicense, today).CountAsync(cancellationToken);

        var licensesPerProvider = await providers
            .Where(p => p.Licenses.Any())
            .OrderByDescending(p => p.Licenses.Count)
            .ThenBy(p => p.ProviderName)
            .Take(TopProvidersByLicenses)
            .Select(p => new ProviderLicenseCount(
                p.ProviderId, p.ProviderName, p.Licenses.Count, p.Licenses.AsQueryable().Count(isValid)))
            .ToListAsync(cancellationToken);

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
                new NamedCount("Other not valid", totalLicenses - validLicenses - expiredLicenses - suspendedLicenses)
                {
                    Key = nameof(LicenseValidity.OtherNotValid)
                }
            ],
            countyCounts.Select(c => new NamedCount(c.County, c.Count)).ToList(),
            licensesPerProvider,
            ExpiringWindowDays,
            expiring
                .Select(l => new ExpiringLicenseDto(
                    l.ProviderId, l.ProviderName, l.LicenseNumber, l.ExpirationDate, l.ExpirationDate.DayNumber - today.DayNumber))
                .ToList());
    }

    public async Task<IReadOnlyList<ProviderListItem>> ProvidersAsync(DashboardProviderQuery query, CancellationToken cancellationToken = default)
    {
        var today = clock.Today;
        var providers = db.Providers.AsNoTracking();

        if (query.Status is { } status)
        {
            providers = providers.Where(p => p.Status == status);
        }

        if (!string.IsNullOrWhiteSpace(query.County))
        {
            // County.Name uses the NOCASE collation, so this matches whatever the casing.
            var county = query.County.Trim();
            providers = providers.Where(p => p.County.Name == county);
        }

        if (query.LicenseGap is { } gap)
        {
            providers = WithGap(providers, gap, today);
        }

        return await providers
            .OrderBy(p => p.ProviderName)
            .ThenBy(p => p.ProviderId)
            .Take(MaxRecords)
            .ToListItems(today)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<DashboardLicenseRow>> LicensesAsync(LicenseValidity? validity, CancellationToken cancellationToken = default)
    {
        var today = clock.Today;
        var licenses = db.Licenses.AsNoTracking();

        if (validity is { } bucket)
        {
            licenses = InBucket(licenses, bucket, today);
        }

        var rows = await licenses
            .Include(l => l.Provider)
            .ThenInclude(p => p.County)
            .OrderBy(l => l.Provider.ProviderName)
            .ThenBy(l => l.ExpirationDate)
            .Take(MaxRecords)
            .ToListAsync(cancellationToken);

        return rows
            .Select(l => new DashboardLicenseRow(
                l.LicenseId,
                l.ProviderId,
                l.Provider.ProviderName,
                l.Provider.County.Name,
                l.LicenseNumber,
                l.LicenseStatus,
                l.IssueDate,
                l.ExpirationDate,
                LicenseRules.IsExpired(l, today),
                LicenseRules.IsCurrentlyValid(l, today),
                LicenseRules.EffectiveStatus(l, today)))
            .ToList();
    }

    // The counts on the summary and the record lists behind them go through these two methods,
    // so a number and the list it opens can never disagree.

    private static IQueryable<Provider> WithGap(IQueryable<Provider> providers, LicenseGap gap, DateOnly today)
    {
        var isValid = LicenseRules.ValidOn(today);
        var isExpired = LicenseRules.ExpiredOn(today);
        var active = providers.Where(p => p.Status == ProviderStatus.Active);

        return gap == LicenseGap.NoValidLicense
            ? active.Where(p => !p.Licenses.AsQueryable().Any(isValid))
            : active.Where(p => p.Licenses.AsQueryable().Any(isExpired));
    }

    private static IQueryable<License> InBucket(IQueryable<License> licenses, LicenseValidity validity, DateOnly today) =>
        validity switch
        {
            LicenseValidity.Valid => licenses.Where(LicenseRules.ValidOn(today)),
            LicenseValidity.Expired => licenses.Where(LicenseRules.ExpiredOn(today)),
            LicenseValidity.Suspended => licenses.Where(
                l => l.LicenseStatus == LicenseStatus.Suspended && l.ExpirationDate >= today),
            _ => licenses.Where(
                l => l.LicenseStatus == LicenseStatus.Expired && l.ExpirationDate >= today)
        };
}
