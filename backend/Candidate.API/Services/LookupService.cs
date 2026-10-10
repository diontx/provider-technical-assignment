using Candidate.API.Contracts;
using Candidate.API.Data;
using Candidate.API.Domain;
using Microsoft.EntityFrameworkCore;

namespace Candidate.API.Services;

public interface ILookupService
{
    Task<IReadOnlyList<CountyDto>> CountiesAsync(CancellationToken cancellationToken = default);
    StatusLookups Statuses();
}

/// <summary>Dropdown values for the UI, so the lists of counties and statuses live in one place.</summary>
public sealed class LookupService(ProviderDbContext db) : ILookupService
{
    public async Task<IReadOnlyList<CountyDto>> CountiesAsync(CancellationToken cancellationToken = default) =>
        await db.Counties
            .AsNoTracking()
            .OrderBy(c => c.Name)
            .Select(c => new CountyDto(c.CountyId, c.Name))
            .ToListAsync(cancellationToken);

    public StatusLookups Statuses() => new(Enum.GetNames<ProviderStatus>(), Enum.GetNames<LicenseStatus>());
}
