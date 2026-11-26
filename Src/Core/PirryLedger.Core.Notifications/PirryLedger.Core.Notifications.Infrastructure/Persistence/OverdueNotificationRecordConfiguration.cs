using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PirryLedger.Core.Notifications.Domain.Entities;

namespace PirryLedger.Core.Notifications.Infrastructure;

internal sealed class OverdueNotificationRecordConfiguration : IEntityTypeConfiguration<OverdueNotificationRecord>
{
    public void Configure(EntityTypeBuilder<OverdueNotificationRecord> builder)
    {
        builder.ToTable("not_overdue_notification_records");
        builder.HasKey(record => record.Id);
        builder.Property(record => record.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(record => record.EventKey).HasColumnName("event_key").HasMaxLength(160).IsRequired();
        builder.Property(record => record.RecordedAtUtc).HasColumnName("recorded_at_utc");
        builder.HasIndex(record => record.EventKey).IsUnique();
    }
}
