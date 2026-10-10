using Candidate.API.Domain;
using Candidate.API.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Candidate.API.Data;

/// <summary>
/// Runs on every SaveChanges. Stamps the audit columns, rotates the concurrency stamp, and
/// turns any delete of an auditable entity into a soft delete, so calling Remove() can never
/// physically delete a Provider or License.
/// </summary>
public sealed class AuditSoftDeleteInterceptor(IClock clock, ICurrentUser currentUser) : SaveChangesInterceptor
{
    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        Apply(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        Apply(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private void Apply(DbContext? context)
    {
        if (context is null)
        {
            return;
        }

        var now = clock.UtcNow;
        var user = currentUser.Name;

        foreach (var entry in context.ChangeTracker.Entries<AuditableEntity>().ToList())
        {
            switch (entry.State)
            {
                case EntityState.Added:
                    // Only fill gaps, so a seeder or import can supply its own audit values.
                    if (entry.Entity.CreatedUtc == default)
                    {
                        entry.Entity.CreatedUtc = now;
                    }

                    if (string.IsNullOrEmpty(entry.Entity.CreatedBy))
                    {
                        entry.Entity.CreatedBy = user;
                    }

                    if (string.IsNullOrEmpty(entry.Entity.ConcurrencyStamp))
                    {
                        entry.Entity.ConcurrencyStamp = NewStamp();
                    }

                    break;

                case EntityState.Deleted:
                    // Unchanged first, so the UPDATE only touches the soft-delete and audit columns.
                    entry.State = EntityState.Unchanged;
                    entry.Property(e => e.IsDeleted).CurrentValue = true;
                    entry.Property(e => e.DeletedUtc).CurrentValue = now;
                    entry.Property(e => e.DeletedBy).CurrentValue = user;
                    StampModified(entry, now, user);
                    break;

                case EntityState.Modified:
                    StampModified(entry, now, user);
                    break;
            }
        }
    }

    private static void StampModified(EntityEntry<AuditableEntity> entry, DateTime now, string user)
    {
        entry.Property(e => e.ModifiedUtc).CurrentValue = now;
        entry.Property(e => e.ModifiedBy).CurrentValue = user;
        entry.Property(e => e.ConcurrencyStamp).CurrentValue = NewStamp();
    }

    private static string NewStamp() => Guid.NewGuid().ToString();
}
