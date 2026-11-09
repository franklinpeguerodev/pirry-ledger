using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PirryLedger.Business.Infrastructure.Persistence;

namespace PirryLedger.Business.Infrastructure.EntityConfigurations;

public sealed class ImageRevisionCounterConfiguration : IEntityTypeConfiguration<ImageRevisionCounter>
{
    public void Configure(EntityTypeBuilder<ImageRevisionCounter> builder)
    {
        builder.ToTable("bil_image_revision");

        builder.HasKey(counter => counter.Id);

        builder.Property(counter => counter.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(counter => counter.Value)
            .HasColumnName("value")
            .IsRequired();
    }
}
