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
    int ExpiringWindowDays,
    IReadOnlyList<ExpiringLicenseDto> ExpiringLicenses);

public sealed record NamedCount(string Name, int Count);

public sealed record ExpiringLicenseDto(
    int ProviderId,
    string ProviderName,
    string LicenseNumber,
    DateOnly ExpirationDate,
    int DaysRemaining);
