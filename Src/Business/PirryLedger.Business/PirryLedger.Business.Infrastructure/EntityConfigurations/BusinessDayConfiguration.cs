using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PirryLedger.Business.Domain.Entities;

namespace PirryLedger.Business.Infrastructure.EntityConfigurations;

public sealed class BusinessDayConfiguration : IEntityTypeConfiguration<BusinessDay>
{
    public void Configure(EntityTypeBuilder<BusinessDay> builder)
    {
        builder.ToTable("bil_business_days");
        builder.HasKey(businessDay => businessDay.Id);
        builder.Property(businessDay => businessDay.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(businessDay => businessDay.BranchId).HasColumnName("branch_id");
        builder.Property(businessDay => businessDay.BusinessDate).HasColumnName("business_date");
        builder.Property(businessDay => businessDay.OpenedAtUtc).HasColumnName("opened_at_utc");
        builder.Property(businessDay => businessDay.IsOpen).HasColumnName("is_open");
        builder.Property(businessDay => businessDay.LastClosedAtUtc).HasColumnName("last_closed_at_utc");
        builder.Property(businessDay => businessDay.LastClosedByUserId).HasColumnName("last_closed_by_user_id");
        builder.Ignore(businessDay => businessDay.IssueDeadlineUtc);
        builder.Ignore(businessDay => businessDay.IsReopened);
        builder.HasIndex(businessDay => businessDay.BranchId).IsUnique().HasFilter("is_open = TRUE")
            .HasDatabaseName("ux_bil_business_days_open_branch");
        builder.OwnsOne(businessDay => businessDay.Closing, closingBuilder =>
        {
            closingBuilder.Property(closing => closing.Sold).HasColumnName("closing_sold").HasPrecision(18, 2);
            closingBuilder.Property(closing => closing.Cash).HasColumnName("closing_cash").HasPrecision(18, 2);
            closingBuilder.Property(closing => closing.ConfirmedTransfers).HasColumnName("closing_confirmed_transfers").HasPrecision(18, 2);
            closingBuilder.Property(closing => closing.PendingTransfers).HasColumnName("closing_pending_transfers").HasPrecision(18, 2);
            closingBuilder.Property(closing => closing.Debt).HasColumnName("closing_debt").HasPrecision(18, 2);
            closingBuilder.Property(closing => closing.PendingCancellation).HasColumnName("closing_pending_cancellation").HasPrecision(18, 2);
            closingBuilder.Property(closing => closing.Cancelled).HasColumnName("closing_cancelled").HasPrecision(18, 2);
            closingBuilder.Property(closing => closing.NotDelivered).HasColumnName("closing_not_delivered").HasPrecision(18, 2);
        });
    }
}
