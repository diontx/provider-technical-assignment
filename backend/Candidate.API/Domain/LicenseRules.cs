using System.Linq.Expressions;

namespace Candidate.API.Domain;

/// <summary>
/// The one place that defines license validity.
/// <list type="bullet">
/// <item>Expired: the expiration date has passed, whatever the stored status says.</item>
/// <item>Currently valid: stored status is Active and the expiration date has not passed.</item>
/// <item>A license is valid through the end of its expiration date.</item>
/// </list>
/// Each rule exists as an expression (translated to SQL by EF Core) and as a method (for
/// objects already in memory). Keep the two in step.
/// </summary>
public static class LicenseRules
{
    public static Expression<Func<License, bool>> ExpiredOn(DateOnly today) =>
        license => license.ExpirationDate < today;

    public static Expression<Func<License, bool>> ValidOn(DateOnly today) =>
        license => license.LicenseStatus == LicenseStatus.Active && license.ExpirationDate >= today;

    public static bool IsExpired(License license, DateOnly today) =>
        license.ExpirationDate < today;

    public static bool IsCurrentlyValid(License license, DateOnly today) =>
        license.LicenseStatus == LicenseStatus.Active && license.ExpirationDate >= today;

    /// <summary>Stored status, overridden to Expired once the expiration date has passed.</summary>
    public static LicenseStatus EffectiveStatus(License license, DateOnly today) =>
        IsExpired(license, today) ? LicenseStatus.Expired : license.LicenseStatus;
}
