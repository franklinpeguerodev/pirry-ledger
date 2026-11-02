using PirryLedger.Core.Contracts.Permissions;
using PirryLedger.Core.Permissions.Domain;

namespace PirryLedger.Core.Permissions.Tests.Catalogo;

// RF-GP-01 + RF-NEG-06: el catalogo acepta el permiso de anular una factura
// concreta con la convencion CancelInvoice:<id> y sigue cerrado para los
// nombres sueltos. La prueba ata las dos implementaciones de la convencion
// (PermissionNames en Core.Contracts y PermissionCatalog en el dominio), que
// se mantienen separadas a proposito para no crear una dependencia nueva
// desde el dominio hacia Contracts.
public sealed class PruebasDelCatalogoDePermisos
{
    [Fact]
    public void ElCatalogoAceptaElPermisoDeAnularUnaFacturaConcreta()
    {
        // Arrange
        var factura = Guid.NewGuid();

        // Act
        var nombre = PermissionNames.CancelInvoice(factura);

        // Assert
        Assert.True(PermissionCatalog.IsKnown(nombre));
        Assert.True(PermissionNames.IsCancelInvoice(nombre, out var recuperada));
        Assert.Equal(factura, recuperada);
    }

    [Fact]
    public void ElNombreDeAnulacionCabeEnLaColumna()
    {
        // Act
        var nombre = PermissionNames.CancelInvoice(Guid.NewGuid());

        // Assert: permission_name es varchar(64) (ADR 008).
        Assert.True(nombre.Length <= 64);
    }

    [Fact]
    public void ElCatalogoRechazaElGuidEnMayusculasParaQueHayaUnSoloNombrePorFactura()
    {
        // Arrange: la misma factura escrita en mayusculas es otro nombre, y
        // dos nombres para la misma factura romperian "un permiso por factura".
        var factura = Guid.Parse("abcdef01-2345-6789-abcd-ef0123456789");
        var enMayusculas = "CancelInvoice:" + factura.ToString("D").ToUpperInvariant();

        // Act + Assert
        Assert.False(PermissionCatalog.IsKnown(enMayusculas));
        Assert.False(PermissionNames.IsCancelInvoice(enMayusculas));
    }

    [Theory]
    [InlineData("CancelInvoice:")]
    [InlineData("CancelInvoice:123")]
    [InlineData("CancelInvoice:no-es-un-guid")]
    [InlineData("CancelInvoice:{11111111-1111-1111-1111-111111111111}")]
    [InlineData("cancelinvoice:11111111-1111-1111-1111-111111111111")]
    [InlineData("CancelInvoice:11111111-1111-1111-1111-111111111111 ")]
    public void ElCatalogoRechazaUnPermisoDeAnulacionMalFormado(string nombre)
    {
        // Act + Assert: el catalogo y el constructor del nombre coinciden.
        Assert.False(PermissionCatalog.IsKnown(nombre));
        Assert.False(PermissionNames.IsCancelInvoice(nombre));
    }

    [Fact]
    public void ElCatalogoSigueRechazandoUnNombreInventado()
    {
        // Act + Assert
        Assert.False(PermissionCatalog.IsKnown("PermisoInventado"));
        Assert.True(PermissionCatalog.IsKnown(PermissionCatalog.ViewSalesSummary));
    }
}
