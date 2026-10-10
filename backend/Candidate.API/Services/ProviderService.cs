using Candidate.API.Contracts;
using Candidate.API.Data;
using Candidate.API.Domain;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Candidate.API.Services;

public interface IProviderService
{
    Task<PagedResult<ProviderListItem>> ListAsync(ProviderQuery query, CancellationToken cancellationToken = default);
    Task<ServiceResult<ProviderDetail>> GetAsync(int providerId, CancellationToken cancellationToken = default);
    Task<ServiceResult<IReadOnlyList<LicenseDto>>> GetLicensesAsync(int providerId, CancellationToken cancellationToken = default);
    Task<ServiceResult<ProviderDetail>> CreateAsync(CreateProviderRequest request, CancellationToken cancellationToken = default);
    Task<ServiceResult<ProviderDetail>> UpdateAsync(int providerId, UpdateProviderRequest request, CancellationToken cancellationToken = default);
    Task<ServiceResult<bool>> DeleteAsync(int providerId, CancellationToken cancellationToken = default);
}

/// <summary>
/// Provider business rules. Nothing here filters on IsDeleted: the DbContext's global query
/// filter does that, so a soft-deleted provider is simply "not found" to every operation.
/// </summary>
public sealed class ProviderService(ProviderDbContext db, IClock clock) : IProviderService
{
    private const int SqliteConstraintViolation = 19;

    public async Task<PagedResult<ProviderListItem>> ListAsync(ProviderQuery query, CancellationToken cancellationToken = default)
    {
        var providers = db.Providers.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            // LIKE is case-insensitive in SQLite. A leading wildcard cannot use IX_Provider_Name,
            // which is acceptable at this table size.
            var pattern = ToContainsPattern(query.Search.Trim());
            providers = providers.Where(p => EF.Functions.Like(p.ProviderName, pattern, @"\"));
        }

        if (query.Status is { } status)
        {
            providers = providers.Where(p => p.Status == status);
        }

        if (query.CountyId is { } countyId)
        {
            providers = providers.Where(p => p.CountyId == countyId);
        }

        var totalCount = await providers.CountAsync(cancellationToken);

        var items = await providers
            .OrderBy(p => p.ProviderName)
            .ThenBy(p => p.ProviderId)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToListItems(clock.Today)
            .ToListAsync(cancellationToken);

        return new PagedResult<ProviderListItem>(items, query.Page, query.PageSize, totalCount);
    }

    public async Task<ServiceResult<ProviderDetail>> GetAsync(int providerId, CancellationToken cancellationToken = default)
    {
        var provider = await db.Providers
            .AsNoTracking()
            .Include(p => p.County)
            .Include(p => p.Licenses)
            .FirstOrDefaultAsync(p => p.ProviderId == providerId, cancellationToken);

        return provider is null ? NotFound(providerId) : provider.ToDetail(clock.Today);
    }

    public async Task<ServiceResult<IReadOnlyList<LicenseDto>>> GetLicensesAsync(int providerId, CancellationToken cancellationToken = default)
    {
        var result = await GetAsync(providerId, cancellationToken);
        return result.IsSuccess ? new ServiceResult<IReadOnlyList<LicenseDto>>(result.Value!.Licenses, null) : result.Error!;
    }

    public async Task<ServiceResult<ProviderDetail>> CreateAsync(CreateProviderRequest request, CancellationToken cancellationToken = default)
    {
        var validated = await ValidateAsync(request, currentProviderId: null, cancellationToken);
        if (!validated.IsSuccess)
        {
            return validated.Error!;
        }

        var provider = new Provider
        {
            ProviderName = validated.Value.Name,
            CountyId = validated.Value.CountyId,
            Status = validated.Value.Status
        };

        db.Providers.Add(provider);

        var saveError = await SaveAsync(cancellationToken);
        return saveError ?? await GetAsync(provider.ProviderId, cancellationToken);
    }

    public async Task<ServiceResult<ProviderDetail>> UpdateAsync(int providerId, UpdateProviderRequest request, CancellationToken cancellationToken = default)
    {
        var provider = await db.Providers.FirstOrDefaultAsync(p => p.ProviderId == providerId, cancellationToken);
        if (provider is null)
        {
            return NotFound(providerId);
        }

        var validated = await ValidateAsync(request, providerId, cancellationToken);
        if (!validated.IsSuccess)
        {
            return validated.Error!;
        }

        // Checked here so an unchanged save (which issues no UPDATE) still reports a stale stamp.
        if (!string.Equals(provider.ConcurrencyStamp, request.ConcurrencyStamp, StringComparison.Ordinal))
        {
            return StaleStamp();
        }

        provider.ProviderName = validated.Value.Name;
        provider.CountyId = validated.Value.CountyId;
        provider.Status = validated.Value.Status;

        // And enforced by the database: the UPDATE carries "WHERE ConcurrencyStamp = <value the client loaded>",
        // which closes the gap between the check above and the write.
        db.Entry(provider).Property(p => p.ConcurrencyStamp).OriginalValue = request.ConcurrencyStamp!;

        var saveError = await SaveAsync(cancellationToken);
        return saveError ?? await GetAsync(providerId, cancellationToken);
    }

    public async Task<ServiceResult<bool>> DeleteAsync(int providerId, CancellationToken cancellationToken = default)
    {
        var provider = await db.Providers.FirstOrDefaultAsync(p => p.ProviderId == providerId, cancellationToken);
        if (provider is null)
        {
            return NotFound(providerId);
        }

        // Remove() is safe: AuditSoftDeleteInterceptor rewrites the delete as an UPDATE that sets IsDeleted.
        db.Providers.Remove(provider);

        var saveError = await SaveAsync(cancellationToken);
        if (saveError is not null)
        {
            return saveError;
        }

        return true;
    }

    private async Task<ServiceResult<(string Name, int CountyId, ProviderStatus Status)>> ValidateAsync(
        ProviderWriteRequest request, int? currentProviderId, CancellationToken cancellationToken)
    {
        // The request attributes already reject most of this before the controller action runs.
        // The checks are repeated because the service is the owner of the rules, not the HTTP layer.
        var name = request.ProviderName?.Trim();
        if (string.IsNullOrEmpty(name))
        {
            return ServiceError.Validation("providerName", "Provider name is required.");
        }

        if (name.Length > Provider.NameMaxLength)
        {
            return ServiceError.Validation("providerName", "Provider name must be 200 characters or fewer.");
        }

        if (!EnumNameAttribute.TryParse<ProviderStatus>(request.Status, out var status))
        {
            return ServiceError.Validation("status", $"Status must be one of: {string.Join(", ", Enum.GetNames<ProviderStatus>())}.");
        }

        var countyId = request.CountyId ?? 0;
        if (!await db.Counties.AnyAsync(c => c.CountyId == countyId, cancellationToken))
        {
            return ServiceError.Validation("countyId", "Select a valid Georgia county.");
        }

        // ProviderName uses the NOCASE collation, so this comparison is case-insensitive in SQL.
        var duplicate = await db.Providers.AnyAsync(
            p => p.ProviderName == name && p.CountyId == countyId && p.ProviderId != (currentProviderId ?? 0),
            cancellationToken);

        if (duplicate)
        {
            return DuplicateName();
        }

        return (name, countyId, status);
    }

    /// <summary>Saves, translating the two expected database failures into business errors.</summary>
    private async Task<ServiceError?> SaveAsync(CancellationToken cancellationToken)
    {
        try
        {
            await db.SaveChangesAsync(cancellationToken);
            return null;
        }
        catch (DbUpdateConcurrencyException)
        {
            return StaleStamp();
        }
        catch (DbUpdateException ex) when (ex.InnerException is SqliteException { SqliteErrorCode: SqliteConstraintViolation })
        {
            // Two requests passed the duplicate check at the same moment; the unique index stopped the second.
            return DuplicateName();
        }
    }

    private static string ToContainsPattern(string search) =>
        "%" + search.Replace(@"\", @"\\").Replace("%", @"\%").Replace("_", @"\_") + "%";

    private static ServiceError NotFound(int providerId) =>
        ServiceError.NotFound($"Provider {providerId} was not found.");

    private static ServiceError StaleStamp() =>
        ServiceError.Conflict("This provider was changed by someone else after you opened it. Reload it and try again.");

    private static ServiceError DuplicateName() =>
        ServiceError.Conflict("A provider with this name already exists in this county.");
}
