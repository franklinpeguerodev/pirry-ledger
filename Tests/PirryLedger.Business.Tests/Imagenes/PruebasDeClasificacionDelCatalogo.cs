using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using PirryLedger.Business.Api.Models;
using PirryLedger.Business.Application.UseCases;

namespace PirryLedger.Business.Tests.Imagenes;

public sealed class PruebasDeClasificacionDelCatalogo
{
    [Fact]
    public async Task ClasificarProductoPermiteFiltrarPorCategoriaYSubcategoria()
    {
        await using var servidor = new ServidorDePruebasHttp();
        var cliente = servidor.Cliente;
        cliente.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", servidor.TokenDeAdministrator());
        var existente = servidor.CrearProducto("Producto existente");
        var alta = await cliente.PostAsJsonAsync("/api/admin/categories", new { name = "Comidas" });
        var categoria = (await alta.Content.ReadFromJsonAsync<CategoryItem>())!;
        var altaSubcategoria = await cliente.PostAsJsonAsync(
            $"/api/admin/categories/{categoria.Id}/subcategories", new { name = "Postres" });
        var subcategoria = (await altaSubcategoria.Content.ReadFromJsonAsync<SubcategoryItem>())!;

        var altaProducto = await cliente.PostAsJsonAsync("/api/admin/products",
            new { name = "Flan", description = "Flan casero", price = 100m, subcategoryId = subcategoria.Id });
        var productos = await cliente.GetFromJsonAsync<ProductResponse[]>($"/api/billing/products?categoryId={categoria.Id}");
        var porSubcategoria = await cliente.GetFromJsonAsync<ProductResponse[]>($"/api/billing/products?subcategoryId={subcategoria.Id}");
        var sinCategoria = await cliente.GetFromJsonAsync<ProductResponse[]>("/api/billing/products?unclassified=true");

        Assert.Equal(HttpStatusCode.Created, altaProducto.StatusCode);
        Assert.Equal(subcategoria.Id, Assert.Single(productos!).SubcategoryId);
        Assert.Equal("Flan", Assert.Single(porSubcategoria!).Name);
        Assert.Equal(existente.Id, Assert.Single(sinCategoria!).Id);
    }

    [Fact]
    public async Task StandardPuedeConsultarClasificacionPeroNoCrearCategorias()
    {
        await using var servidor = new ServidorDePruebasHttp();
        var cliente = servidor.Cliente;
        cliente.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", servidor.TokenDeStandard());

        var respuesta = await cliente.PostAsJsonAsync("/api/admin/categories", new { name = "Postres" });
        var categorias = await cliente.GetFromJsonAsync<CategoryItem[]>("/api/billing/categories");

        Assert.Equal(HttpStatusCode.Forbidden, respuesta.StatusCode);
        Assert.Empty(categorias!);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RechazarEdicionInvalidaConservaLosDatosDelProducto(bool subcategoriaInexistente)
    {
        await using var servidor = new ServidorDePruebasHttp();
        var cliente = servidor.Cliente;
        cliente.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", servidor.TokenDeAdministrator());
        var producto = servidor.CrearProducto();

        var respuesta = await cliente.PutAsJsonAsync($"/api/admin/products/{producto.Id}", new
        {
            name = "Nombre nuevo",
            description = subcategoriaInexistente ? "Descripción nueva" : "  ",
            price = 1m,
            subcategoryId = subcategoriaInexistente ? Guid.NewGuid() : (Guid?)null
        });

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
        Assert.Equal("Combo Pirri", producto.Name);
        Assert.Equal("Producto de prueba", producto.Description);
        Assert.Equal(500m, producto.Price);
        Assert.Null(producto.SubcategoryId);
    }
}
