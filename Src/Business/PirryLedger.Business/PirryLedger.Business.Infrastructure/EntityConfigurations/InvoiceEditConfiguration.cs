using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PirryLedger.Business.Domain.Entities;

namespace PirryLedger.Business.Infrastructure.EntityConfigurations;

public sealed class InvoiceEditConfiguration : IEntityTypeConfiguration<InvoiceEdit>
{
    public void Configure(EntityTypeBuilder<InvoiceEdit> builder)
    {
        builder.ToTable("bil_invoice_edits");

        builder.HasKey(edit => edit.Id);

        builder.Property(edit => edit.Id).HasColumnName("id");

        builder.Property(edit => edit.InvoiceId).HasColumnName("invoice_id");

        builder.Property(edit => edit.EditedAtUtc).HasColumnName("edited_at_utc");

        builder.Property(edit => edit.EditedByUserId).HasColumnName("edited_by_user_id");

        builder.Property(edit => edit.ItemCount).HasColumnName("item_count");
    }
}
