using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PirryLedger.Core.Permissions.Domain.Entities;

namespace PirryLedger.Core.Permissions.Infrastructure.EntityConfigurations;

// Tabla per_permission_grants (ADR 008). La fila se inserta cuando una
// solicitud pasa a Applied; el chequeo de IPermissionAuthority pregunta
// EXISTS sobre esta tabla.
//
// Indice unico (user_id, permission_name): un usuario no puede tener dos
// concesiones activas del mismo permiso. Es una segunda barrera ademas de
// la transicion de la maquina de estados: aunque alguien apruebe dos veces
// la misma solicitud, la BD rechaza el segundo INSERT.
internal sealed class PermissionGrantConfiguration : IEntityTypeConfiguration<PermissionGrant>
{
    public void Configure(EntityTypeBuilder<PermissionGrant> builder)
    {
        builder.ToTable("per_permission_grants");

        builder.HasKey(grant => grant.Id);

        builder.Property(grant => grant.Id)
            .HasColumnName("id");

        builder.Property(grant => grant.UserId)
            .HasColumnName("user_id")
            .IsRequired();

        builder.Property(grant => grant.PermissionName)
            .HasColumnName("permission_name")
            .HasMaxLength(64)
            .IsRequired();

        builder.Property(grant => grant.GrantedAtUtc)
            .HasColumnName("granted_at_utc")
            .IsRequired();

        builder.Property(grant => grant.GrantedByUserId)
            .HasColumnName("granted_by_user_id");

        // Las dos claves foraneas hacia ac_users se declaran en la migracion
        // por la misma razon que en PermissionRequestConfiguration: este modulo
        // no depende de AccessControl.Domain.Entities.User.

        builder.HasIndex(grant => new { grant.UserId, grant.PermissionName })
            .IsUnique()
            .HasDatabaseName("ux_per_permission_grants_user_permission");

        builder.HasIndex(grant => grant.PermissionName)
            .HasDatabaseName("ix_per_permission_grants_permission");
    }
}