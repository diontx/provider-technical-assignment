using Candidate.API.Contracts;
using Candidate.API.Data;
using Microsoft.EntityFrameworkCore;

namespace Candidate.API.Services;

public interface IAuditService
{
    Task<IReadOnlyList<DeletedProviderDto>> DeletedProvidersAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// The only place in the application that bypasses the soft-delete query filter.
/// Keeping it to one class makes "who can see deleted data" easy to answer and to secure later.
/// </summary>
public sealed class AuditService(ProviderDbContext db) : IAuditService
{
    public async Task<IReadOnlyList<DeletedProviderDto>> DeletedProvidersAsync(CancellationToken cancellationToken = default) =>
        await db.Providers
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(p => p.IsDeleted)
            .OrderByDescending(p => p.DeletedUtc)
            .Select(p => new DeletedProviderDto(
                p.ProviderId,
                p.ProviderName,
                p.County.Name,
                p.Status,
                p.CreatedUtc,
                p.CreatedBy,
                p.DeletedUtc,
                p.DeletedBy,
                p.Licenses.Count))
            .ToListAsync(cancellationToken);
}
