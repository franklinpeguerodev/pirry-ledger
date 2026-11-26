using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PirryLedger.Core.Notifications.Domain.Entities;

namespace PirryLedger.Core.Notifications.Infrastructure;

internal sealed class NotificationConfiguration : IEntityTypeConfiguration<Notification>
{
    public void Configure(EntityTypeBuilder<Notification> builder)
    {
        builder.ToTable("not_notifications");
        builder.HasKey(notification => notification.Id);
        builder.Property(notification => notification.Id).HasColumnName("id");
        builder.Property(notification => notification.RecipientUserId).HasColumnName("recipient_user_id");
        builder.Property(notification => notification.Subject).HasColumnName("subject").HasMaxLength(200).IsRequired();
        builder.Property(notification => notification.Body).HasColumnName("body").IsRequired();
        builder.Property(notification => notification.CreatedAtUtc).HasColumnName("created_at_utc");
        builder.Property(notification => notification.ReadAtUtc).HasColumnName("read_at_utc");
        builder.Property(notification => notification.EventKey).HasColumnName("event_key").HasMaxLength(160);
        builder.HasIndex(notification => new { notification.EventKey, notification.RecipientUserId }).IsUnique();
        builder.HasIndex(notification => new { notification.RecipientUserId, notification.CreatedAtUtc });
    }
}
