using Candidate.API.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Candidate.API.Data.Configurations;

internal sealed class ProviderConfiguration : IEntityTypeConfiguration<Provider>
{
    public void Configure(EntityTypeBuilder<Provider> builder)
    {
        builder.ConfigureAuditable("Provider");
        builder.ToTable(t =>
        {
            t.HasCheckConstraint("CK_Provider_Status", "Status IN ('Active', 'Inactive', 'Pending')");
            t.HasCheckConstraint("CK_Provider_ProviderName", "length(trim(ProviderName)) BETWEEN 1 AND 200");
        });

        builder.HasKey(p => p.ProviderId);

        // NOCASE makes equality, ordering, and the unique index below case-insensitive.
        builder.Property(p => p.ProviderName).IsRequired().HasMaxLength(Provider.NameMaxLength).UseCollation("NOCASE");
        builder.Property(p => p.Status).IsRequired().HasConversion<string>().HasMaxLength(20);

        builder.HasOne(p => p.County)
            .WithMany()
            .HasForeignKey(p => p.CountyId)
            .OnDelete(DeleteBehavior.Restrict);

        // Partial indexes: only live rows are indexed, and a soft-deleted provider
        // does not block re-creating the same name in the same county.
        builder.HasIndex(p => p.ProviderName)
            .HasDatabaseName("IX_Provider_Name")
            .HasFilter(AuditableEntityConfiguration.NotDeletedFilter);

        builder.HasIndex(p => new { p.Status, p.CountyId })
            .HasDatabaseName("IX_Provider_Status_County")
            .HasFilter(AuditableEntityConfiguration.NotDeletedFilter);

        builder.HasIndex(p => new { p.ProviderName, p.CountyId })
            .IsUnique()
            .HasDatabaseName("UX_Provider_Name_County")
            .HasFilter(AuditableEntityConfiguration.NotDeletedFilter);

        // Soft delete, layer 1: every query through this DbContext excludes deleted providers
        // unless it opts out with IgnoreQueryFilters().
        builder.HasQueryFilter(p => !p.IsDeleted);
    }
}
