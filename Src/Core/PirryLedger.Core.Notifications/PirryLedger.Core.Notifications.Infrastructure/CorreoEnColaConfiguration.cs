using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PirryLedger.Core.Notifications.Domain;

namespace PirryLedger.Core.Notifications.Infrastructure;

// El prefijo not_ en la tabla deja claro que las dos piezas comparten base de
// datos pero no tablas: AccessControl tiene sus propias con prefijo ac_.
internal sealed class CorreoEnColaConfiguration : IEntityTypeConfiguration<CorreoEnCola>
{
    public void Configure(EntityTypeBuilder<CorreoEnCola> builder)
    {
        builder.ToTable("not_correos_en_cola");

        builder.HasKey(correo => correo.Id);

        builder.Property(correo => correo.Id)
            .HasColumnName("id");

        builder.Property(correo => correo.Destinatario)
            .HasColumnName("destinatario")
            .HasMaxLength(320)
            .IsRequired();

        builder.Property(correo => correo.Asunto)
            .HasColumnName("asunto")
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(correo => correo.Cuerpo)
            .HasColumnName("cuerpo")
            .IsRequired();

        builder.Property(correo => correo.Estado)
            .HasColumnName("estado")
            .IsRequired();

        builder.Property(correo => correo.Intentos)
            .HasColumnName("intentos")
            .IsRequired();

        // timestamptz: la fecha siempre se guarda en UTC y se lee en UTC (RD-11).
        builder.Property(correo => correo.FechaCreacionUtc)
            .HasColumnName("fecha_creacion_utc")
            .HasColumnType("timestamp with time zone")
            .IsRequired();

        builder.Property(correo => correo.FechaEnvioUtc)
            .HasColumnName("fecha_envio_utc")
            .HasColumnType("timestamp with time zone");

        builder.Property(correo => correo.UltimoError)
            .HasColumnName("ultimo_error");

        // El emisor filtra por estado y ordena por fecha de creacion.
        builder.HasIndex(correo => new { correo.Estado, correo.FechaCreacionUtc })
            .HasDatabaseName("ix_not_correos_en_cola_estado_fecha_creacion");
    }
}
