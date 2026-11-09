using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PirryLedger.Business.Domain.Entities;

namespace PirryLedger.Business.Infrastructure.EntityConfigurations;

public sealed class ProductImageConfiguration : IEntityTypeConfiguration<ProductImage>
{
    public void Configure(EntityTypeBuilder<ProductImage> builder)
    {
        builder.ToTable("bil_product_images");

        builder.HasKey(image => image.Id);

        builder.Property(image => image.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(image => image.ProductId)
            .HasColumnName("product_id")
            .IsRequired();

        builder.HasOne<Product>()
            .WithMany()
            .HasForeignKey(image => image.ProductId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Property(image => image.ImageKey)
            .HasColumnName("image_key")
            .HasMaxLength(ProductImage.MaximumKeyLength)
            .IsRequired();

        builder.Property(image => image.Version)
            .HasColumnName("version")
            .HasMaxLength(ProductImage.MaximumVersionLength)
            .IsRequired();

        builder.Property(image => image.ContentType)
            .HasColumnName("content_type")
            .HasMaxLength(ProductImage.MaximumContentTypeLength)
            .IsRequired();

        builder.Property(image => image.SizeBytes)
            .HasColumnName("size_bytes")
            .IsRequired();

        builder.Property(image => image.RevisionAdded)
            .HasColumnName("revision_added")
            .IsRequired();

        builder.Property(image => image.RevisionRemoved)
            .HasColumnName("revision_removed");

        builder.Property(image => image.CreatedAtUtc)
            .HasColumnName("created_at_utc")
            .IsRequired();

        builder.Property(image => image.RemovedAtUtc)
            .HasColumnName("removed_at_utc");

        // Una sola imagen vigente por producto. El indice es parcial porque las
        // imagenes que ya salieron del conjunto se quedan como historial y
        // pueden ser varias para el mismo producto.
        builder.HasIndex(image => image.ProductId)
            .IsUnique()
            .HasFilter("revision_removed IS NULL")
            .HasDatabaseName("ux_bil_product_images_vigente");

        builder.HasIndex(image => image.RevisionAdded)
            .HasDatabaseName("ix_bil_product_images_revision_added");

        builder.HasIndex(image => image.RevisionRemoved)
            .HasDatabaseName("ix_bil_product_images_revision_removed");
    }
}
