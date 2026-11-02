using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PirryLedger.Business.Domain.Entities;

namespace PirryLedger.Business.Infrastructure.EntityConfigurations;

public sealed class InvoiceItemConfiguration : IEntityTypeConfiguration<InvoiceItem>
{
    public void Configure(EntityTypeBuilder<InvoiceItem> builder)
    {
        builder.ToTable("bil_invoice_items");

        builder.HasKey(item => item.Id);

        builder.Property(item => item.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(item => item.InvoiceId).HasColumnName("invoice_id");

        builder.Property(item => item.Name).HasColumnName("name").HasMaxLength(120).IsRequired();

        builder.Property(item => item.UnitPrice).HasColumnName("unit_price").HasColumnType("numeric(12,2)");

        builder.Property(item => item.Quantity).HasColumnName("quantity");

        builder.Ignore(item => item.LineTotal);
    }
}
