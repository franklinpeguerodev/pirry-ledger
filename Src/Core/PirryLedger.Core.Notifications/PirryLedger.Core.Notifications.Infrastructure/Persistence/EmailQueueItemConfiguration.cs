using PirryLedger.Core.Notifications.Application;
using PirryLedger.Core.Notifications.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PirryLedger.Core.Notifications.Domain.Entities;

namespace PirryLedger.Core.Notifications.Infrastructure;

// El prefijo not_ en la tabla deja claro que las dos piezas comparten base de
// datos pero no tablas: AccessControl tiene sus propias con prefijo ac_.
internal sealed class EmailQueueItemConfiguration : IEntityTypeConfiguration<EmailQueueItem>
{
    public void Configure(EntityTypeBuilder<EmailQueueItem> builder)
    {
        builder.ToTable("not_email_queue");

        builder.HasKey(email => email.Id);

        builder.Property(email => email.Id)
            .HasColumnName("id");

        builder.Property(email => email.Recipient)
            .HasColumnName("recipient")
            .HasMaxLength(320)
            .IsRequired();

        builder.Property(email => email.Subject)
            .HasColumnName("subject")
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(email => email.Body)
            .HasColumnName("body")
            .IsRequired();

        builder.Property(email => email.Status)
            .HasColumnName("status")
            .IsRequired();

        builder.Property(email => email.Attempts)
            .HasColumnName("attempts")
            .IsRequired();

        // timestamptz: la fecha siempre se guarda en UTC y se lee en UTC (RD-11).
        builder.Property(email => email.CreatedAtUtc)
            .HasColumnName("created_at_utc")
            .HasColumnType("timestamp with time zone")
            .IsRequired();

        builder.Property(email => email.SentAtUtc)
            .HasColumnName("sent_at_utc")
            .HasColumnType("timestamp with time zone");

        builder.Property(email => email.LastError)
            .HasColumnName("last_error");

        // El emisor filtra por Status y ordena por fecha de creacion.
        builder.HasIndex(email => new { email.Status, email.CreatedAtUtc })
            .HasDatabaseName("ix_not_email_queue_status_created_at");
    }
}
