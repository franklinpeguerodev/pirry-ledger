using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PirryLedger.Business.Domain.Entities;

namespace PirryLedger.Business.Infrastructure.EntityConfigurations;

public sealed class ProductConfiguration : IEntityTypeConfiguration<Product>
{
    public void Configure(EntityTypeBuilder<Product> builder)
    {
        builder.ToTable("bil_products");

        builder.HasKey(product => product.Id);

        builder.Property(product => product.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(product => product.Name)
            .HasColumnName("name")
            .HasMaxLength(Product.MaximumNameLength)
            .IsRequired();

        builder.Property(product => product.Description)
            .HasColumnName("description")
            .HasMaxLength(Product.MaximumDescriptionLength);

        builder.Property(product => product.Price)
            .HasColumnName("price")
            .HasColumnType("numeric(12,2)")
            .IsRequired();

        builder.Property(product => product.IsActive)
            .HasColumnName("is_active")
            .IsRequired();

        // La revision del ultimo cambio de activo a inactivo o al reves. Sirve
        // para que el cliente reciba la imagen cuando el producto se reactiva
        // (ADR 015).
        builder.Property(product => product.ImageRevision)
            .HasColumnName("image_revision")
            .IsRequired();

        builder.HasIndex(product => product.Name)
            .IsUnique();
    }
}
