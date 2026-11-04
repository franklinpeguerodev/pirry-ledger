using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PirryLedger.Core.AccessControl.Domain.Entities;

namespace PirryLedger.Core.AccessControl.Infrastructure;

internal sealed class BranchConfiguration : IEntityTypeConfiguration<Branch>
{
    public void Configure(EntityTypeBuilder<Branch> builder)
    {
        builder.ToTable("ac_branches");
        builder.HasKey(branch => branch.Id);
        builder.Property(branch => branch.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(branch => branch.Name).HasColumnName("name").HasMaxLength(120).IsRequired();
        builder.Property(branch => branch.Address).HasColumnName("address").HasMaxLength(100).IsRequired();
        builder.Property(branch => branch.IsAll).HasColumnName("is_all").IsRequired();
        builder.Property(branch => branch.Active).HasColumnName("active").IsRequired();
        builder.HasIndex(branch => branch.Name).IsUnique();
    }
}
