using Candidate.API.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Candidate.API.Data.Configurations;

internal sealed class CountyConfiguration : IEntityTypeConfiguration<County>
{
    public void Configure(EntityTypeBuilder<County> builder)
    {
        builder.ToTable("County");
        builder.HasKey(c => c.CountyId);
        builder.Property(c => c.CountyId).ValueGeneratedNever();
        builder.Property(c => c.Name).IsRequired().HasMaxLength(100).UseCollation("NOCASE");
        builder.HasIndex(c => c.Name).IsUnique().HasDatabaseName("UX_County_Name");

        // Reference data belongs with the schema, so it ships in the migration rather than the sample-data seed.
        builder.HasData(GeorgiaCounties.Names.Select((name, index) => new County { CountyId = index + 1, Name = name }));
    }
}
