using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PirryLedger.Core.AccessControl.Domain;

namespace PirryLedger.Core.AccessControl.Infrastructure;

internal sealed class TokenActivacionConfiguration : IEntityTypeConfiguration<TokenActivacion>
{
    public void Configure(EntityTypeBuilder<TokenActivacion> builder)
    {
        builder.ToTable("ac_tokens_activacion");

        builder.HasKey(token => token.Id);

        builder.Property(token => token.HashDelToken)
            .HasMaxLength(128)
            .IsRequired();

        builder.Property(token => token.EmitidoUtc).IsRequired();
        builder.Property(token => token.ExpiraUtc).IsRequired();

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