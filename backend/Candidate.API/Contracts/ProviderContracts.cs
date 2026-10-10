using System.ComponentModel.DataAnnotations;
using Candidate.API.Domain;

namespace Candidate.API.Contracts;

// Responses. Entities never leave the API, so audit and soft-delete columns are exposed only on purpose.

public sealed record ProviderListItem(
    int ProviderId,
    string ProviderName,
    int CountyId,
    string County,
    ProviderStatus Status,
    DateTime CreatedUtc,
    DateTime? ModifiedUtc,
    int LicenseCount,
    int ValidLicenseCount,
    int ExpiredLicenseCount,
    string ConcurrencyStamp);

public sealed record ProviderDetail(
    int ProviderId,
    string ProviderName,
    int CountyId,
    string County,
    ProviderStatus Status,
    DateTime CreatedUtc,
    string CreatedBy,
    DateTime? ModifiedUtc,
    string? ModifiedBy,
    string ConcurrencyStamp,
    DateOnly AsOfDate,
    bool HasValidLicense,
    IReadOnlyList<LicenseDto> Licenses);

/// <param name="LicenseStatus">Status as stored in the database.</param>
/// <param name="IsExpired">Computed: the expiration date is before <c>AsOfDate</c>.</param>
/// <param name="IsCurrentlyValid">Computed: stored status is Active and the license has not expired.</param>
/// <param name="EffectiveStatus">Stored status, shown as Expired once the expiration date has passed.</param>
public sealed record LicenseDto(
    int LicenseId,
    int ProviderId,
    string LicenseNumber,
    LicenseStatus LicenseStatus,
    DateOnly? IssueDate,
    DateOnly ExpirationDate,
    bool IsExpired,
    bool IsCurrentlyValid,
    LicenseStatus EffectiveStatus);

public sealed record PagedResult<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalCount)
{
    public int TotalPages => (int)Math.Ceiling(TotalCount / (double)PageSize);
}

// Requests.

public sealed class ProviderQuery
{
    public const int MaxPageSize = 100;

    /// <summary>Case-insensitive "contains" match on the provider name.</summary>
    [StringLength(Provider.NameMaxLength)]
    public string? Search { get; set; }

    public ProviderStatus? Status { get; set; }

    public int? CountyId { get; set; }

    [Range(1, 100_000, ErrorMessage = "Page must be between 1 and 100000.")]
    public int Page { get; set; } = 1;

    [Range(1, MaxPageSize, ErrorMessage = "Page size must be between 1 and 100.")]
    public int PageSize { get; set; } = 20;
}

public abstract class ProviderWriteRequest
{
    [Required(ErrorMessage = "Provider name is required.")]
    [StringLength(Provider.NameMaxLength, ErrorMessage = "Provider name must be 200 characters or fewer.")]
    public string? ProviderName { get; set; }

    [Required(ErrorMessage = "County is required.")]
    [Range(1, int.MaxValue, ErrorMessage = "County is required.")]
    public int? CountyId { get; set; }

    // A string rather than the enum, so an unknown value produces a field-level validation
    // message instead of a JSON deserialization error.
    [Required(ErrorMessage = "Status is required.")]
    [EnumName(typeof(ProviderStatus))]
    public string? Status { get; set; }
}

public sealed class CreateProviderRequest : ProviderWriteRequest;

public sealed class UpdateProviderRequest : ProviderWriteRequest
{
    /// <summary>The value returned when the provider was loaded. A stale value is rejected with 409.</summary>
    [Required(ErrorMessage = "Concurrency stamp is required. Reload the provider and try again.")]
    public string? ConcurrencyStamp { get; set; }
}
