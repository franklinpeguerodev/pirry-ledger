using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PirryLedger.Business.Domain.Entities;
using PirryLedger.Business.Domain.Enums;

namespace PirryLedger.Business.Infrastructure.EntityConfigurations;

public sealed class InvoicePaymentConfiguration : IEntityTypeConfiguration<InvoicePayment>
{
    public void Configure(EntityTypeBuilder<InvoicePayment> builder)
    {
        builder.ToTable("bil_invoice_payments");

        builder.HasKey(payment => payment.Id);

        builder.Property(payment => payment.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(payment => payment.InvoiceId).HasColumnName("invoice_id");

        builder.Property(payment => payment.Kind).HasColumnName("kind").HasConversion<int>();

        builder.Property(payment => payment.Method).HasColumnName("method").HasConversion<int>();

        builder.Property(payment => payment.Status).HasColumnName("status").HasConversion<int>();

        builder.Property(payment => payment.Amount).HasColumnName("amount").HasColumnType("numeric(12,2)");

        builder.Property(payment => payment.ReceivedAmount).HasColumnName("received_amount").HasColumnType("numeric(12,2)");

        builder.Property(payment => payment.ChangeAmount).HasColumnName("change_amount").HasColumnType("numeric(12,2)");

        builder.Property(payment => payment.RegisteredAtUtc).HasColumnName("registered_at_utc");

        builder.Property(payment => payment.RegisteredByUserId).HasColumnName("registered_by_user_id");
    }
}
