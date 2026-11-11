using Microsoft.EntityFrameworkCore;
using PirryLedger.Core.Documents.Domain.Entities;
using PirryLedger.Core.Documents.Domain.Rules;
using PirryLedger.Core.Documents.Infrastructure.Persistence;

namespace PirryLedger.Core.Documents.Tests.Persistencia;

// RD-12: el repositorio real se ejercita sobre EF Core en memoria, sin
// PostgreSQL. Aqui se comprueba el alta y el borrado logico tal como quedan en
// la base, que es donde aparecio el fallo: un alta guardada con Update no
// inserta nada y EF responde DbUpdateConcurrencyException.
public sealed class PruebasDelRepositorioDeDocumentos
{
    private static readonly Guid Uploader = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid Branch = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly DateTime UploadedAt = new(2026, 11, 10, 15, 30, 0, DateTimeKind.Utc);

    [Fact]
    public async Task GuardarUnDocumentoNuevoLoDejaEnLaBase()
    {
        await using var context = CreateContext();
        var repository = new DocumentRepository(context);
        var document = CreateDocument();

        await repository.AddAsync(document);

        context.ChangeTracker.Clear();
        var stored = await repository.FindAsync(document.Id);

        Assert.NotNull(stored);
        Assert.Equal("factura.pdf", stored!.Name);
        Assert.Equal("application/pdf", stored.ContentType);
        Assert.Equal(20, stored.SizeBytes);
        Assert.Equal(Uploader, stored.UploadedByUserId);
        Assert.Equal(Branch, stored.BranchId);
    }

    [Fact]
    public async Task ActualizarUnDocumentoEliminadoGuardaLaFechaYElUsuario()
    {
        await using var context = CreateContext();
        var repository = new DocumentRepository(context);
        var document = CreateDocument();
        await repository.AddAsync(document);

        context.ChangeTracker.Clear();
        var loaded = await repository.FindAsync(document.Id);
        Assert.NotNull(loaded);

        loaded!.Delete(Uploader, UploadedAt.AddMinutes(5));
        await repository.UpdateAsync(loaded);

        context.ChangeTracker.Clear();
        var stored = await repository.FindAsync(document.Id);

        Assert.True(stored!.IsDeleted);
        Assert.Equal(Uploader, stored.DeletedByUserId);
        Assert.Equal(UploadedAt.AddMinutes(5), stored.DeletedAtUtc);
    }

    [Fact]
    public async Task ElListadoDeUnUsuarioNoDevuelveLosEliminados()
    {
        await using var context = CreateContext();
        var repository = new DocumentRepository(context);

        var vigente = CreateDocument("vigente.pdf");
        var eliminado = CreateDocument("eliminado.pdf");
        await repository.AddAsync(vigente);
        await repository.AddAsync(eliminado);

        context.ChangeTracker.Clear();
        var toDelete = await repository.FindAsync(eliminado.Id);
        toDelete!.Delete(Uploader, UploadedAt.AddMinutes(1));
        await repository.UpdateAsync(toDelete);

        context.ChangeTracker.Clear();
        var documents = await repository.ListByOwnerAsync(Uploader);

        var document = Assert.Single(documents);
        Assert.Equal("vigente.pdf", document.Name);
    }

    private static DocumentDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<DocumentDbContext>()
            .UseInMemoryDatabase($"documents-{Guid.NewGuid()}")
            .Options;

        return new DocumentDbContext(options);
    }

    private static Document CreateDocument(string name = "factura.pdf") =>
        Document.Create(
            name,
            "application/pdf",
            20,
            Uploader,
            Branch,
            $"documents/{Guid.NewGuid()}",
            UploadedAt,
            DocumentRules.DefaultMaxSizeBytes);
}
