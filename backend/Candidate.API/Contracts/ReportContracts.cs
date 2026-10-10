using Candidate.API.Domain;

namespace Candidate.API.Contracts;

/// <param name="AsOfDate">The business date (US Eastern) the report was evaluated against.</param>
public sealed record ProviderLicenseReport(DateOnly AsOfDate, IReadOnlyList<ProviderLicenseReportRow> Items);

/// <param name="HasNoValidLicense">True when the provider holds no currently valid license at all.</param>
/// <param name="Licenses">Only the licenses relevant to the report (the valid ones, or the expired ones).</param>
public sealed record ProviderLicenseReportRow(
    int ProviderId,
    string ProviderName,
    string County,
    ProviderStatus Status,
    bool HasNoValidLicense,
    IReadOnlyList<LicenseDto> Licenses);

public sealed record DeletedProviderDto(
    int ProviderId,
    string ProviderName,
    string County,
    ProviderStatus Status,
    DateTime CreatedUtc,
    string CreatedBy,
    DateTime? DeletedUtc,
    string? DeletedBy,
    int LicenseCount);

public sealed record CountyDto(int CountyId, string Name);

public sealed record StatusLookups(IReadOnlyList<string> ProviderStatuses, IReadOnlyList<string> LicenseStatuses);

public sealed record DashboardSummary(
    DateOnly AsOfDate,
    int TotalProviders,
    int TotalLicenses,
    int ActiveProvidersWithoutValidLicense,
    int ActiveProvidersWithExpiredLicense,
    IReadOnlyList<NamedCount> ProvidersByStatus,
    IReadOnlyList<NamedCount> LicensesByEffectiveStatus,
    IReadOnlyList<NamedCount> ProvidersByCounty,
    IReadOnlyList<ProviderLicenseCount> LicensesPerProvider,
    int ExpiringWindowDays,
    IReadOnlyList<ExpiringLicenseDto> ExpiringLicenses);

/// <param name="Name">Label to display.</param>
public sealed record NamedCount(string Name, int Count)
{
    /// <summary>Value to send back to the API to list the records behind this count. Defaults to the name.</summary>
    public string Key { get; init; } = Name;
}

public sealed record ProviderLicenseCount(int ProviderId, string ProviderName, int LicenseCount, int ValidLicenseCount);

/// <summary>The license problems the dashboard counts. Both apply to providers whose status is Active.</summary>
public enum LicenseGap
{
    /// <summary>Active provider with no currently valid license.</summary>
    NoValidLicense,

    /// <summary>Active provider with at least one expired license.</summary>
    ExpiredLicense
}

/// <summary>The dashboard's license buckets. Every license falls in exactly one.</summary>
public enum LicenseValidity
{
    Valid,
    Expired,
    Suspended,

    /// <summary>Not expired by date, but stored with a status other than Active or Suspended.</summary>
    OtherNotValid
}

/// <summary>Which providers to list behind a dashboard number. All filters are optional and combine.</summary>
public sealed class DashboardProviderQuery
{
    public ProviderStatus? Status { get; set; }

    /// <summary>County name, as shown on the dashboard.</summary>
    [System.ComponentModel.DataAnnotations.StringLength(100)]
    public string? County { get; set; }

    public LicenseGap? LicenseGap { get; set; }
}

/// <summary>A license with the provider it belongs to, for the dashboard's read-only record lists.</summary>
public sealed record DashboardLicenseRow(
    int LicenseId,
    int ProviderId,
    string ProviderName,
    string County,
    string LicenseNumber,
    LicenseStatus LicenseStatus,
    DateOnly? IssueDate,
    DateOnly ExpirationDate,
    bool IsExpired,
    bool IsCurrentlyValid,
    LicenseStatus EffectiveStatus);

public sealed record ExpiringLicenseDto(
    int ProviderId,
    string ProviderName,
    string LicenseNumber,
    DateOnly ExpirationDate,
    int DaysRemaining);
