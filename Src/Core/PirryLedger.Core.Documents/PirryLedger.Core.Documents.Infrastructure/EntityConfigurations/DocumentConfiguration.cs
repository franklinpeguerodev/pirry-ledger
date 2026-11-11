using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PirryLedger.Core.Documents.Domain.Entities;
using PirryLedger.Core.Documents.Domain.Rules;

namespace PirryLedger.Core.Documents.Infrastructure.EntityConfigurations;

// Tabla doc_documents (ADR 017, decision 7). Los nombres de columna van en
// snake_case (ADR 004).
//
// Los ids de usuario y de sucursal son valores planos: no hay clave foranea
// hacia las tablas de otra pieza, porque cada modulo es duenio de las suyas y
// la existencia del usuario la garantiza la sesion ya validada.
internal sealed class DocumentConfiguration : IEntityTypeConfiguration<Document>
{
    public void Configure(EntityTypeBuilder<Document> builder)
    {
        builder.ToTable("doc_documents");

        builder.HasKey(document => document.Id);

        builder.Property(document => document.Id)
            .HasColumnName("id");

        builder.Property(document => document.Name)
            .HasColumnName("name")
            .HasMaxLength(DocumentRules.MaxNameLength)
            .IsRequired();

        builder.Property(document => document.ContentType)
            .HasColumnName("content_type")
            .HasMaxLength(DocumentRules.MaxNameLength)
            .IsRequired();

        builder.Property(document => document.SizeBytes)
            .HasColumnName("size_bytes")
            .IsRequired();

        builder.Property(document => document.UploadedByUserId)
            .HasColumnName("uploaded_by")
            .IsRequired();

        builder.Property(document => document.BranchId)
            .HasColumnName("branch_id")
            .IsRequired();

        builder.Property(document => document.StorageKey)
            .HasColumnName("storage_key")
            .HasMaxLength(DocumentRules.MaxNameLength)
            .IsRequired();

        builder.Property(document => document.UploadedAtUtc)
            .HasColumnName("uploaded_at_utc")
            .IsRequired();

        builder.Property(document => document.DeletedAtUtc)
            .HasColumnName("deleted_at_utc");

        builder.Property(document => document.DeletedByUserId)
            .HasColumnName("deleted_by");

        // Propiedad calculada: no es una columna.
        builder.Ignore(document => document.IsDeleted);

        // Una clave de almacenamiento no se repite nunca: es un Guid por subida.
        builder.HasIndex(document => document.StorageKey)
            .IsUnique()
            .HasDatabaseName("ux_doc_documents_storage_key");

        // El listado de un Standard filtra por duenio y ordena por fecha.
        builder.HasIndex(document => new { document.UploadedByUserId, document.UploadedAtUtc })
            .HasDatabaseName("ix_doc_documents_uploader");

        // El listado excluye eliminados.
        builder.HasIndex(document => document.DeletedAtUtc)
            .HasDatabaseName("ix_doc_documents_deleted_at");
    }
}
