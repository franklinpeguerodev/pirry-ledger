using PirryLedger.Core.AccessControl.Application;
using PirryLedger.Core.AccessControl.Domain.Entities;
using PirryLedger.Core.AccessControl.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace PirryLedger.Core.AccessControl.Infrastructure;

internal sealed class SessionConfiguration : IEntityTypeConfiguration<Session>
{
    public void Configure(EntityTypeBuilder<Session> builder)
    {
        builder.ToTable("ac_sessions");

        // Columnas en snake_case (ADR 004), igual que en el resto de tablas ac_.
        builder.HasKey(session => session.Id);

        builder.Property(session => session.Id)
            .HasColumnName("id");

        builder.Property(session => session.UserId)
            .HasColumnName("user_id");

        // El token en claro SI va en base64url, pero lo que se guarda es su SHA-256 en
        // hexadecimal: 64 caracteres. Se deja margen como en el resto de hashes del
        // proyecto.
        builder.Property(session => session.TokenHash)
            .HasColumnName("token_hash")
            .HasMaxLength(128)
            .IsRequired();

        builder.Property(session => session.IssuedAtUtc)
            .HasColumnName("issued_at_utc")
            .IsRequired();

        builder.Property(session => session.ExpiresAtUtc)
            .HasColumnName("expires_at_utc")
            .IsRequired();

        builder.Property(session => session.ClosedAtUtc)
            .HasColumnName("closed_at_utc");

        builder.Property(session => session.CredentialVersion)
            .HasColumnName("credential_version")
            .IsRequired();

        // Unico sobre el hash: dos sesiones con el mismo token no pueden existir.
        // El token sale de 32 bytes del generador del sistema, asi que la
        // colision es practicamente imposible, pero el indice hace que sea
        // imposible en lugar de improbablemente improbable.
        //
        // Este indice es tambien el del camino caliente: el punto de validacion
        // busca por hash en cada peticion autenticada.
        builder.HasIndex(session => session.TokenHash)
            .IsUnique()
            .HasDatabaseName("ix_ac_sessions_hash");

        // Este indice no es para el camino caliente: es para la limpieza de
        // sesiones vencidas que describe el ADR. Sin el, limpiar seria un WHERE
        // sobre la tabla entera.
        builder.HasIndex(session => session.ExpiresAtUtc)
            .HasDatabaseName("ix_ac_sessions_expires");

        builder.HasIndex(session => session.UserId)
            .HasDatabaseName("ix_ac_sessions_user");

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(session => session.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
