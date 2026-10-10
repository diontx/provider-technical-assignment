using Candidate.API.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Candidate.API.Data.Configurations;

internal sealed class LicenseConfiguration : IEntityTypeConfiguration<License>
{
    public void Configure(EntityTypeBuilder<License> builder)
    {
        builder.ConfigureAuditable("License");
        builder.ToTable(t =>
        {
            t.HasCheckConstraint("CK_License_LicenseStatus", "LicenseStatus IN ('Active', 'Expired', 'Suspended')");
            t.HasCheckConstraint("CK_License_LicenseNumber", "length(trim(LicenseNumber)) BETWEEN 1 AND 50");
        });

        builder.HasKey(l => l.LicenseId);

        builder.Property(l => l.LicenseNumber).IsRequired().HasMaxLength(License.NumberMaxLength).UseCollation("NOCASE");
        builder.Property(l => l.LicenseStatus).IsRequired().HasConversion<string>().HasMaxLength(20);

        // Restrict, never cascade: providers are not physically deleted, and a license must keep its provider.
        builder.HasOne(l => l.Provider)
            .WithMany(p => p.Licenses)
            .HasForeignKey(l => l.ProviderId)
            .OnDelete(DeleteBehavior.Restrict);

        // Deliberately not filtered on IsDeleted: a license number is never reused for the same provider.
        builder.HasIndex(l => new { l.ProviderId, l.LicenseNumber })
            .IsUnique()
            .HasDatabaseName("UX_License_Provider_Number");

        builder.HasIndex(l => new { l.ProviderId, l.ExpirationDate })
            .HasDatabaseName("IX_License_Provider_Expiration")
            .HasFilter(AuditableEntityConfiguration.NotDeletedFilter);

        // A license is hidden when it is deleted or when its provider is. Going through the
        // provider avoids copying the delete flag onto each license, which keeps restore simple.
        builder.HasQueryFilter(l => !l.IsDeleted && !l.Provider.IsDeleted);
    }
}
