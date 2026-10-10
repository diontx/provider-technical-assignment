using Candidate.API.Contracts;
using Candidate.API.Data;
using Candidate.API.Domain;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Candidate.API.Services;

public interface ILicenseService
{
    /// <summary>Issues a new Active license to a provider that has no currently valid license.</summary>
    Task<ServiceResult<LicenseDto>> IssueAsync(int providerId, IssueLicenseRequest request, CancellationToken cancellationToken = default);
}

public sealed class LicenseService(ProviderDbContext db, IClock clock) : ILicenseService
{
    public const string NumberPrefix = "CCLC-";
    public const int NumberOffset = 10_000;
    /// <summary>The default term, and also the shortest term a license can be issued for.</summary>
    public const int MinimumTermYears = 1;

    private const int SqliteConstraintViolation = 19;

    /// <summary>The license number for a license id, for example 13 gives "CCLC-10013".</summary>
    public static string FormatNumber(int licenseId) => $"{NumberPrefix}{NumberOffset + licenseId}";

    public async Task<ServiceResult<LicenseDto>> IssueAsync(int providerId, IssueLicenseRequest request, CancellationToken cancellationToken = default)
    {
        var today = clock.Today;

        // A soft-deleted provider is excluded by the query filter, so it cannot be issued a license.
        var provider = await db.Providers
            .Include(p => p.Licenses)
            .FirstOrDefaultAsync(p => p.ProviderId == providerId, cancellationToken);
        if (provider is null)
        {
            return ServiceError.NotFound($"Provider {providerId} was not found.");
        }

        var issueDate = request.IssueDate ?? today;
        var expirationDate = request.ExpirationDate ?? issueDate.AddYears(MinimumTermYears);

        // A license issued in the future would be stored as Active and counted as valid before it starts.
        if (issueDate > today)
        {
            return ServiceError.Validation("issueDate", "Issue date cannot be in the future.");
        }

        if (expirationDate < issueDate.AddYears(MinimumTermYears))
        {
            return ServiceError.Validation("expirationDate", "Expiration date must be at least one year after the issue date.");
        }

        if (expirationDate < today)
        {
            return ServiceError.Validation("expirationDate", "Expiration date cannot be in the past.");
        }

        var valid = provider.Licenses.FirstOrDefault(l => LicenseRules.IsCurrentlyValid(l, today));
        if (valid is not null)
        {
            return ServiceError.Conflict(
                $"This provider already holds a valid license ({valid.LicenseNumber}, expires {valid.ExpirationDate:yyyy-MM-dd}).");
        }

        var license = new License
        {
            ProviderId = providerId,
            // Replaced below, in the same transaction, once the database has assigned the id.
            LicenseNumber = $"PENDING-{Guid.NewGuid():N}",
            LicenseStatus = LicenseStatus.Active,
            IssueDate = issueDate,
            ExpirationDate = expirationDate
        };

        try
        {
            await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);

            db.Licenses.Add(license);
            await db.SaveChangesAsync(cancellationToken);

            // The number is derived from the identity value, so two requests at the same moment can
            // never be given the same number. "Highest existing number plus one" could be.
            // ExecuteUpdate keeps this an edit of the row being created, not an audited modification.
            var number = FormatNumber(license.LicenseId);
            await db.Licenses
                .Where(l => l.LicenseId == license.LicenseId)
                .ExecuteUpdateAsync(setters => setters.SetProperty(l => l.LicenseNumber, number), cancellationToken);

            await transaction.CommitAsync(cancellationToken);

            return license.ToDto(today) with { LicenseNumber = number };
        }
        catch (Exception ex) when (IsConstraintViolation(ex))
        {
            // Only possible if this provider was given the same number by hand; the transaction rolled back.
            return ServiceError.Conflict("A license number could not be assigned to this provider. Contact support.");
        }
    }

    private static bool IsConstraintViolation(Exception exception) =>
        (exception as SqliteException ?? exception.InnerException as SqliteException)?.SqliteErrorCode == SqliteConstraintViolation;
}
