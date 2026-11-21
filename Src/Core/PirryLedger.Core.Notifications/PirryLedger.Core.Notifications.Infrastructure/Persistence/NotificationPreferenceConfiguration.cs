using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PirryLedger.Core.Notifications.Domain.Entities;

namespace PirryLedger.Core.Notifications.Infrastructure;

internal sealed class NotificationPreferenceConfiguration : IEntityTypeConfiguration<NotificationPreference>
{
    public void Configure(EntityTypeBuilder<NotificationPreference> builder)
    {
        builder.ToTable("not_notification_preferences");
        builder.HasKey(preference => preference.UserId);
        builder.Property(preference => preference.UserId).HasColumnName("user_id");
        builder.Property(preference => preference.EmailEnabled).HasColumnName("email_enabled");
        builder.Property(preference => preference.UpdatedAtUtc).HasColumnName("updated_at_utc");
    }
}
