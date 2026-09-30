using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PirryLedger.Core.AccessControl.Domain;

namespace PirryLedger.Core.AccessControl.Infrastructure;

// Tablas ac_*. AccessControl es la unica pieza que las lee y las escribe:
// ninguna otra las toca (RD-01).
//
// El indice unico sobre Correo es lo que hace cumplir RF-CA-01 de verdad. La
// comprobacion en el codigo da un mensaje bonito; el indice garantiza que dos
// registros concurrentes no metean el mismo correo, porque la base rechaza el
// segundo aunque las dos peticiones pasen la validacion a la vez.
internal sealed class UsuarioConfiguration : IEntityTypeConfiguration<Usuario>
{
    public void Configure(EntityTypeBuilder<Usuario> builder)
    {
        builder.ToTable("ac_usuarios");

        builder.HasKey(usuario => usuario.Id);

        builder.Property(usuario => usuario.Nombre)
            .HasMaxLength(150)
            .IsRequired();

        // RF-CA-01: correo unico garantizado por la base, no solo por el codigo.
        builder.Property(usuario => usuario.Correo)
            .HasMaxLength(320)
            .IsRequired();

        builder.HasIndex(usuario => usuario.Correo)
            .IsUnique()
            .HasDatabaseName("ix_ac_usuarios_correo");

        // RF-CA-02: el hash no se lista en ningun endpoint (RF-CA-21).
        builder.Property(usuario => usuario.HashDeContrasena)
            .HasMaxLength(512)
            .IsRequired();

        builder.Property(usuario => usuario.Rol)
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(usuario => usuario.Activo).IsRequired();
        builder.Property(usuario => usuario.FechaDeCreacionUtc).IsRequired();
        builder.Property(usuario => usuario.CredencialVersion).IsRequired();
        builder.Property(usuario => usuario.IntentosFallidos).IsRequired();
    }
}