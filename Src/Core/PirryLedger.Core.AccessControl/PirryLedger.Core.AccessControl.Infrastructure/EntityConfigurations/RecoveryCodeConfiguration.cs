using PirryLedger.Core.AccessControl.Application;
using PirryLedger.Core.AccessControl.Domain.Entities;
using PirryLedger.Core.AccessControl.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace PirryLedger.Core.AccessControl.Infrastructure;

internal sealed class RecoveryCodeConfiguration : IEntityTypeConfiguration<RecoveryCode>
{
    public void Configure(EntityTypeBuilder<RecoveryCode> builder)
    {
        builder.ToTable("ac_recovery_codes");

        // Columnas en snake_case (ADR 004), igual que en el resto de tablas ac_.
        builder.HasKey(code => code.Id);

        builder.Property(code => code.Id)
            .HasColumnName("id");

        builder.Property(code => code.UserId)
            .HasColumnName("user_id");

        builder.Property(code => code.CodeHash)
            .HasColumnName("code_hash")
            .HasMaxLength(128)
            .IsRequired();

        builder.Property(code => code.IssuedAtUtc)
            .HasColumnName("issued_at_utc")
            .IsRequired();

        builder.Property(code => code.ExpiresAtUtc)
            .HasColumnName("expires_at_utc")
            .IsRequired();

        builder.Property(code => code.UsedAtUtc)
            .HasColumnName("used_at_utc");

        builder.HasIndex(code => code.CodeHash)
            .IsUnique()
            .HasDatabaseName("ix_ac_recovery_codes_hash");

        builder.HasIndex(code => code.UserId)
            .HasDatabaseName("ix_ac_recovery_codes_user");

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(code => code.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
