using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PirryLedger.Core.AccessControl.Domain;

namespace PirryLedger.Core.AccessControl.Infrastructure;

internal sealed class CodigoRecuperacionConfiguration : IEntityTypeConfiguration<CodigoRecuperacion>
{
    public void Configure(EntityTypeBuilder<CodigoRecuperacion> builder)
    {
        builder.ToTable("ac_codigos_recuperacion");

        builder.HasKey(codigo => codigo.Id);

        builder.Property(codigo => codigo.HashDelCodigo)
            .HasMaxLength(128)
            .IsRequired();

        builder.Property(codigo => codigo.EmitidoUtc).IsRequired();
        builder.Property(codigo => codigo.ExpiraUtc).IsRequired();

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