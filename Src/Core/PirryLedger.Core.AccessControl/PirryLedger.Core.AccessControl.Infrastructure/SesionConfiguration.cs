using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PirryLedger.Core.AccessControl.Domain;

namespace PirryLedger.Core.AccessControl.Infrastructure;

internal sealed class SesionConfiguration : IEntityTypeConfiguration<Sesion>
{
    public void Configure(EntityTypeBuilder<Sesion> builder)
    {
        builder.ToTable("ac_sesiones");

        builder.HasKey(sesion => sesion.Id);

        // El token en claro SI va en base64url, pero lo que se guarda es su SHA-256 en
        // hexadecimal: 64 caracteres. Se deja margen como en el resto de hashes del
        // proyecto.
        builder.Property(sesion => sesion.HashDelToken)
            .HasMaxLength(128)
            .IsRequired();

        builder.Property(sesion => sesion.EmitidaUtc).IsRequired();
        builder.Property(sesion => sesion.ExpiraUtc).IsRequired();
        builder.Property(sesion => sesion.CredencialVersion).IsRequired();

        // Unico sobre el hash: dos sesiones con el mismo token no pueden existir.
        // El token sale de 32 bytes del generador del sistema, asi que la
        // colision es practicamente imposible, pero el indice hace que sea
        // imposible en lugar de improbablemente improbable.
        //
        // Este indice es tambien el del camino caliente: el punto de validacion
        // busca por hash en cada peticion autenticada.
        builder.HasIndex(sesion => sesion.HashDelToken)
            .IsUnique()
            .HasDatabaseName("ix_ac_sesiones_hash");

        // Este indice no es para el camino caliente: es para la limpieza de
        // sesiones vencidas que describe el ADR. Sin el, limpiar seria un WHERE
        // sobre la tabla entera.
        builder.HasIndex(sesion => sesion.ExpiraUtc)
            .HasDatabaseName("ix_ac_sesiones_expira");

        builder.HasIndex(sesion => sesion.UsuarioId)
            .HasDatabaseName("ix_ac_sesiones_usuario");

        builder.HasOne<Usuario>()
            .WithMany()
            .HasForeignKey(sesion => sesion.UsuarioId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}