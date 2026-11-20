using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PirryLedger.Business.Domain.Entities;

namespace PirryLedger.Business.Infrastructure.EntityConfigurations;

public sealed class BusinessDayConfiguration : IEntityTypeConfiguration<BusinessDay>
{
    public void Configure(EntityTypeBuilder<BusinessDay> builder)
    {
        builder.ToTable("bil_business_days");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(x => x.BranchId).HasColumnName("branch_id");
        builder.Property(x => x.BusinessDate).HasColumnName("business_date");
        builder.Property(x => x.OpenedAtUtc).HasColumnName("opened_at_utc");
        builder.Property(x => x.IsOpen).HasColumnName("is_open");
        builder.Property(x => x.LastClosedAtUtc).HasColumnName("last_closed_at_utc");
        builder.Property(x => x.LastClosedByUserId).HasColumnName("last_closed_by_user_id");
        builder.Ignore(x => x.IssueDeadlineUtc);
        builder.Ignore(x => x.IsReopened);
        builder.HasIndex(x => x.BranchId).IsUnique().HasFilter("is_open = TRUE")
            .HasDatabaseName("ux_bil_business_days_open_branch");
        builder.OwnsOne(x => x.Closing, closing =>
        {
            closing.Property(x => x.Sold).HasColumnName("closing_sold").HasPrecision(18, 2);
            closing.Property(x => x.Cash).HasColumnName("closing_cash").HasPrecision(18, 2);
            closing.Property(x => x.ConfirmedTransfers).HasColumnName("closing_confirmed_transfers").HasPrecision(18, 2);
            closing.Property(x => x.PendingTransfers).HasColumnName("closing_pending_transfers").HasPrecision(18, 2);
            closing.Property(x => x.Debt).HasColumnName("closing_debt").HasPrecision(18, 2);
            closing.Property(x => x.PendingCancellation).HasColumnName("closing_pending_cancellation").HasPrecision(18, 2);
            closing.Property(x => x.Cancelled).HasColumnName("closing_cancelled").HasPrecision(18, 2);
            closing.Property(x => x.NotDelivered).HasColumnName("closing_not_delivered").HasPrecision(18, 2);
        });
    }
}
