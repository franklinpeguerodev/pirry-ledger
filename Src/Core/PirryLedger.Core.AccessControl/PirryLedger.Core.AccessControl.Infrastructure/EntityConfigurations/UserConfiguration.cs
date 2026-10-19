using PirryLedger.Core.AccessControl.Application;
using PirryLedger.Core.AccessControl.Domain.Entities;
using PirryLedger.Core.AccessControl.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace PirryLedger.Core.AccessControl.Infrastructure;

// Tablas ac_*. AccessControl es la unica pieza que las lee y las escribe:
// ninguna otra las toca (RD-01).
//
// El indice unico sobre Email es lo que hace cumplir RF-CA-01 de verdad. La
// comprobacion en el codigo da un message bonito; el indice garantiza que dos
// registros concurrentes no metean el mismo Email, porque la base rechaza el
// segundo aunque las dos peticiones pasen la validacion a la vez.
internal sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("ac_users");

        // Columnas en snake_case (ADR 004). PostgreSQL pliega los identificadores
        // sin comillas a minusculas: con este Name ninguna consulta manual
        // necesita comillas dobles y las columnas quedan igual que las tablas.
        builder.HasKey(user => user.Id);

        builder.Property(user => user.Id)
            .HasColumnName("id");

        builder.Property(user => user.Name)
            .HasColumnName("name")
            .HasMaxLength(150)
            .IsRequired();

        // RF-CA-01: Email unico garantizado por la base, no solo por el codigo.
        builder.Property(user => user.Email)
            .HasColumnName("email")
            .HasMaxLength(320)
            .IsRequired();

        builder.HasIndex(user => user.Email)
            .IsUnique()
            .HasDatabaseName("ix_ac_users_email");

        // RF-CA-02: el hash no se lista en ningun endpoint (RF-CA-21).
        builder.Property(user => user.PasswordHash)
            .HasColumnName("password_hash")
            .HasMaxLength(512)
            .IsRequired();

        builder.Property(user => user.Role)
            .HasColumnName("role")
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(user => user.Active)
            .HasColumnName("active")
            .IsRequired();

        builder.Property(user => user.CreatedAtUtc)
            .HasColumnName("created_at_utc")
            .IsRequired();

        builder.Property(user => user.PasswordChangedAtUtc)
            .HasColumnName("password_changed_at_utc");

        builder.Property(user => user.CredentialVersion)
            .HasColumnName("credential_version")
            .IsRequired();

        builder.Property(user => user.FailedAttempts)
            .HasColumnName("failed_attempts")
            .IsRequired();

        builder.Property(user => user.LockedUntilUtc)
            .HasColumnName("locked_until_utc");
    }
}
