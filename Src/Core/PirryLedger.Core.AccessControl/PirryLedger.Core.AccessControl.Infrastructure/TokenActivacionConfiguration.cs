using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PirryLedger.Core.AccessControl.Domain;

namespace PirryLedger.Core.AccessControl.Infrastructure;

internal sealed class TokenActivacionConfiguration : IEntityTypeConfiguration<TokenActivacion>
{
    public void Configure(EntityTypeBuilder<TokenActivacion> builder)
    {
        builder.ToTable("ac_tokens_activacion");

        // Columnas en snake_case (ADR 004), igual que en el resto de tablas ac_.
        builder.HasKey(token => token.Id);

        builder.Property(token => token.Id)
            .HasColumnName("id");

        builder.Property(token => token.UsuarioId)
            .HasColumnName("usuario_id");

        builder.Property(token => token.HashDelToken)
            .HasColumnName("hash_del_token")
            .HasMaxLength(128)
            .IsRequired();

        builder.Property(token => token.EmitidoUtc)
            .HasColumnName("emitido_utc")
            .IsRequired();

        builder.Property(token => token.ExpiraUtc)
            .HasColumnName("expira_utc")
            .IsRequired();

        builder.Property(token => token.UsadoUtc)
            .HasColumnName("usado_utc");

        // Un indice unico sobre el hash hace imposible guardar dos veces el
        // mismo token, que es lo que haria que un enlace de un solo uso se
        // pudiera abrir mas de una vez por duplicado (RF-CA-16).
        builder.HasIndex(token => token.HashDelToken)
            .IsUnique()
            .HasDatabaseName("ix_ac_tokens_activacion_hash");

        builder.HasIndex(token => token.UsuarioId)
            .HasDatabaseName("ix_ac_tokens_activacion_usuario");

        builder.HasOne<Usuario>()
            .WithMany()
            .HasForeignKey(token => token.UsuarioId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}