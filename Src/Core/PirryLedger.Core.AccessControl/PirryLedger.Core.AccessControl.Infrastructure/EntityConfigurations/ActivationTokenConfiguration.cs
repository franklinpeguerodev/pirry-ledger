using PirryLedger.Core.AccessControl.Application;
using PirryLedger.Core.AccessControl.Domain.Entities;
using PirryLedger.Core.AccessControl.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace PirryLedger.Core.AccessControl.Infrastructure;

internal sealed class ActivationTokenConfiguration : IEntityTypeConfiguration<ActivationToken>
{
    public void Configure(EntityTypeBuilder<ActivationToken> builder)
    {
        builder.ToTable("ac_activation_tokens");

        // Columnas en snake_case (ADR 004), igual que en el resto de tablas ac_.
        builder.HasKey(token => token.Id);

        builder.Property(token => token.Id)
            .HasColumnName("id");

        builder.Property(token => token.UserId)
            .HasColumnName("user_id");

        builder.Property(token => token.TokenHash)
            .HasColumnName("token_hash")
            .HasMaxLength(128)
            .IsRequired();

        builder.Property(token => token.IssuedAtUtc)
            .HasColumnName("issued_at_utc")
            .IsRequired();

        builder.Property(token => token.ExpiresAtUtc)
            .HasColumnName("expires_at_utc")
            .IsRequired();

        builder.Property(token => token.UsedAtUtc)
            .HasColumnName("used_at_utc");

        // Un indice unico sobre el hash hace imposible guardar dos veces el
        // mismo token, que es lo que haria que un enlace de un solo uso se
        // pudiera abrir mas de una vez por duplicado (RF-CA-16).
        builder.HasIndex(token => token.TokenHash)
            .IsUnique()
            .HasDatabaseName("ix_ac_activation_tokens_hash");

        builder.HasIndex(token => token.UserId)
            .HasDatabaseName("ix_ac_activation_tokens_user");

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(token => token.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
