using Candidate.API.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Candidate.API.Data.Configurations;

internal static class AuditableEntityConfiguration
{
    public const string NotDeletedFilter = "IsDeleted = 0";

    /// <summary>Maps the table and the audit, soft-delete, and concurrency columns shared by Provider and License.</summary>
    public static void ConfigureAuditable<T>(this EntityTypeBuilder<T> builder, string table)
        where T : AuditableEntity
    {
        builder.ToTable(table, t =>
        {
            t.HasCheckConstraint($"CK_{table}_IsDeleted", "IsDeleted IN (0, 1)");

            // The flag and the timestamp must agree, so neither can be trusted alone and be wrong.
            t.HasCheckConstraint(
                $"CK_{table}_SoftDelete",
                "(IsDeleted = 0 AND DeletedUtc IS NULL) OR (IsDeleted = 1 AND DeletedUtc IS NOT NULL)");
        });

        builder.Property(e => e.CreatedBy).IsRequired().HasMaxLength(100);
        builder.Property(e => e.ModifiedBy).HasMaxLength(100);
        builder.Property(e => e.DeletedBy).HasMaxLength(100);
        builder.Property(e => e.IsDeleted).HasDefaultValue(false);
        builder.Property(e => e.ConcurrencyStamp).IsRequired().HasMaxLength(36).IsConcurrencyToken();
    }
}
