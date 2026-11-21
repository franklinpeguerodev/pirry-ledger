using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PirryLedger.Core.Permissions.Domain.Entities;
using PirryLedger.Core.Permissions.Domain.Enums;

namespace PirryLedger.Core.Permissions.Infrastructure.EntityConfigurations;

// Tabla per_permission_requests. AccessControl es dueño de usuarios y sucursales.
// Se conservan identificadores y sucursal histórica, sin entidades ni relaciones
// entre módulos.
//
// Columnas en snake_case (ADR 004): mismas reglas que AccessControl.
internal sealed class PermissionRequestConfiguration : IEntityTypeConfiguration<PermissionRequest>
{
    public void Configure(EntityTypeBuilder<PermissionRequest> builder)
    {
        builder.ToTable("per_permission_requests");

        builder.HasKey(request => request.Id);
        builder.Property(request => request.BranchId).HasColumnName("branch_id");

        builder.Property(request => request.Id)
            .HasColumnName("id");

        builder.Property(request => request.RequesterId)
            .HasColumnName("requester_id")
            .IsRequired();

        builder.Property(request => request.RequestedPermission)
            .HasColumnName("requested_permission")
            .HasMaxLength(64)
            .IsRequired();

        builder.Property(request => request.State)
            .HasColumnName("state")
            .HasConversion<string>()
            .HasMaxLength(16)
            .IsRequired();

        builder.Property(request => request.ApproverId)
            .HasColumnName("approver_id");

        builder.Property(request => request.RejectionReason)
            .HasColumnName("rejection_reason")
            .HasMaxLength(500);

        builder.Property(request => request.CreatedAtUtc)
            .HasColumnName("created_at_utc")
            .IsRequired();

        builder.Property(request => request.ApprovedAtUtc)
            .HasColumnName("approved_at_utc");

        builder.Property(request => request.AppliedAtUtc)
            .HasColumnName("applied_at_utc");

        builder.Property(request => request.RejectedAtUtc)
            .HasColumnName("rejected_at_utc");

        // Las columnas que apuntan a un usuario (requester_id, approver_id) NO
        // declaran clave foranea hacia ac_users, ni aqui ni en la migracion.
        // Declararla con propiedad de navegacion haria que este modulo
        // dependiera de PirryLedger.Core.AccessControl.Domain.Entities.User, que
        // es justo la dependencia entre piezas que se quiere evitar. La
        // integridad la aporta la aplicacion, que toma las dos identidades de
        // la sesion autenticada y nunca del cuerpo de la peticion.
        // La sucursal también se recibe por contrato y se conserva al crear la solicitud.

        // Indices para las dos consultas del caso de uso:
        //   ListByStateAsync(state) -> WHERE state = ...
        //   ListByRequesterAsync(requester_id) -> WHERE requester_id = ...
        builder.HasIndex(request => request.State)
            .HasDatabaseName("ix_per_permission_requests_state");

        builder.HasIndex(request => request.RequesterId)
            .HasDatabaseName("ix_per_permission_requests_requester");
    }
}
