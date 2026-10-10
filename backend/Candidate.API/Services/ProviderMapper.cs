using Candidate.API.Contracts;
using Candidate.API.Domain;

namespace Candidate.API.Services;

internal static class ProviderMapper
{
    public static LicenseDto ToDto(this License license, DateOnly today) => new(
        license.LicenseId,
        license.ProviderId,
        license.LicenseNumber,
        license.LicenseStatus,
        license.IssueDate,
        license.ExpirationDate,
        LicenseRules.IsExpired(license, today),
        LicenseRules.IsCurrentlyValid(license, today),
        LicenseRules.EffectiveStatus(license, today));

    public static IReadOnlyList<LicenseDto> ToDtos(this IEnumerable<License> licenses, DateOnly today) =>
        licenses
            .OrderBy(l => l.ExpirationDate)
            .ThenBy(l => l.LicenseNumber, StringComparer.OrdinalIgnoreCase)
            .Select(l => l.ToDto(today))
            .ToList();

    /// <summary>
    /// The list row, with license counts computed in SQL by the shared license rules.
    /// Used by the provider list and by the dashboard, so both always count the same way.
    /// </summary>
    public static IQueryable<ProviderListItem> ToListItems(this IQueryable<Provider> providers, DateOnly today)
    {
        var isValid = LicenseRules.ValidOn(today);
        var isExpired = LicenseRules.ExpiredOn(today);

        return providers.Select(p => new ProviderListItem(
            p.ProviderId,
            p.ProviderName,
            p.CountyId,
            p.County.Name,
            p.Status,
            p.CreatedUtc,
            p.ModifiedUtc,
            p.Licenses.Count,
            p.Licenses.AsQueryable().Count(isValid),
            p.Licenses.AsQueryable().Count(isExpired),
            p.ConcurrencyStamp));
    }

    /// <summary>Expects County and Licenses to be loaded.</summary>
    public static ProviderDetail ToDetail(this Provider provider, DateOnly today) => new(
        provider.ProviderId,
        provider.ProviderName,
        provider.CountyId,
        provider.County.Name,
        provider.Status,
        provider.CreatedUtc,
        provider.CreatedBy,
        provider.ModifiedUtc,
        provider.ModifiedBy,
        provider.ConcurrencyStamp,
        today,
        provider.Licenses.Any(l => LicenseRules.IsCurrentlyValid(l, today)),
        provider.Licenses.ToDtos(today));
}
