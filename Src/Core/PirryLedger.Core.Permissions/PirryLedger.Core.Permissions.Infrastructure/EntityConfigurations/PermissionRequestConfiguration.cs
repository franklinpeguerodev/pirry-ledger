using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PirryLedger.Core.Permissions.Domain.Entities;
using PirryLedger.Core.Permissions.Domain.Enums;

namespace PirryLedger.Core.Permissions.Infrastructure.EntityConfigurations;

// Tabla per_permission_requests. AccessControl es duenia de ac_users; aqui
// solo declaramos la FK por Guid y por nombre de tabla, sin propiedad de
// navegacion. PirryLedger.Core.Permissions.Infrastructure no ve
// PirryLedger.Core.AccessControl.Domain.Entities.User, asi que la FK no
// arrastra una dependencia entre piezas.
//
// Columnas en snake_case (ADR 004): mismas reglas que AccessControl.
internal sealed class PermissionRequestConfiguration : IEntityTypeConfiguration<PermissionRequest>
{
    public void Configure(EntityTypeBuilder<PermissionRequest> builder)
    {
        builder.ToTable("per_permission_requests");

        builder.HasKey(request => request.Id);

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

        // Las dos claves foraneas (requester_id, approver_id) hacia ac_users
        // se declaran en la migracion, no aqui: este modulo no depende de
        // PirryLedger.Core.AccessControl.Domain.Entities.User (la FK llega por
        // nombre de tabla y tipo Guid, sin propiedad de navegacion). Sin la
        // declaracion aqui, EF genera la tabla con las columnas pero sin
        // restricciones, y la migracion anade el AddForeignKey.

        // Indices para las dos consultas del caso de uso:
        //   ListByStateAsync(state) -> WHERE state = ...
        //   ListByRequesterAsync(requester_id) -> WHERE requester_id = ...
        builder.HasIndex(request => request.State)
            .HasDatabaseName("ix_per_permission_requests_state");

        builder.HasIndex(request => request.RequesterId)
            .HasDatabaseName("ix_per_permission_requests_requester");
    }
}