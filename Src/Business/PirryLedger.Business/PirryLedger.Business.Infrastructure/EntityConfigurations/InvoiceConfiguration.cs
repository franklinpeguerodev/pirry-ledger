using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PirryLedger.Business.Domain.Entities;

namespace PirryLedger.Business.Infrastructure.EntityConfigurations;

public sealed class InvoiceConfiguration : IEntityTypeConfiguration<Invoice>
{
    public void Configure(EntityTypeBuilder<Invoice> builder)
    {
        builder.ToTable("bil_invoices");

        builder.HasKey(invoice => invoice.Id);

        builder.Property(invoice => invoice.Id).HasColumnName("id");

        builder.Property(invoice => invoice.State)
            .HasColumnName("state")
            .HasConversion<int>();

        builder.Property(invoice => invoice.IssuedAtUtc).HasColumnName("issued_at_utc");

        builder.Property(invoice => invoice.IsDelivery).HasColumnName("is_delivery");

        builder.Property(invoice => invoice.DebtorName).HasColumnName("debtor_name").HasMaxLength(120);

        builder.HasMany(invoice => invoice.Items)
            .WithOne()
            .HasForeignKey(item => item.InvoiceId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(invoice => invoice.Payments)
            .WithOne()
            .HasForeignKey(payment => payment.InvoiceId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(invoice => invoice.Edits)
            .WithOne()
            .HasForeignKey(edit => edit.InvoiceId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Ignore(invoice => invoice.Total);
        builder.Ignore(invoice => invoice.PaidAmount);
    }
}
