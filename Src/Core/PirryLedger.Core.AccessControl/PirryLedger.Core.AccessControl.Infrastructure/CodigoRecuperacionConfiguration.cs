using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PirryLedger.Core.AccessControl.Domain;

namespace PirryLedger.Core.AccessControl.Infrastructure;

internal sealed class CodigoRecuperacionConfiguration : IEntityTypeConfiguration<CodigoRecuperacion>
{
    public void Configure(EntityTypeBuilder<CodigoRecuperacion> builder)
    {
        builder.ToTable("ac_codigos_recuperacion");

        // Columnas en snake_case (ADR 004), igual que en el resto de tablas ac_.
        builder.HasKey(codigo => codigo.Id);

        builder.Property(codigo => codigo.Id)
            .HasColumnName("id");

        builder.Property(codigo => codigo.UsuarioId)
            .HasColumnName("usuario_id");

        builder.Property(codigo => codigo.HashDelCodigo)
            .HasColumnName("hash_del_codigo")
            .HasMaxLength(128)
            .IsRequired();

        builder.Property(codigo => codigo.EmitidoUtc)
            .HasColumnName("emitido_utc")
            .IsRequired();

        builder.Property(codigo => codigo.ExpiraUtc)
            .HasColumnName("expira_utc")
            .IsRequired();

        builder.Property(codigo => codigo.UsadoUtc)
            .HasColumnName("usado_utc");

        builder.HasIndex(codigo => codigo.HashDelCodigo)
            .IsUnique()
            .HasDatabaseName("ix_ac_codigos_recuperacion_hash");

        builder.HasIndex(codigo => codigo.UsuarioId)
            .HasDatabaseName("ix_ac_codigos_recuperacion_usuario");

        builder.HasOne<Usuario>()
            .WithMany()
            .HasForeignKey(codigo => codigo.UsuarioId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}