using PirryLedger.Business.Application.Ports;
using PirryLedger.Business.Domain.Entities;

namespace PirryLedger.Business.Tests.Imagenes;

internal sealed class RepositorioDeClasificacionEnMemoria : IClassificationRepository
{
    private readonly List<Category> _categories = [];
    private readonly List<Subcategory> _subcategories = [];

    public Task<IReadOnlyList<Category>> ListCategoriesAsync(CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<Category>>(_categories.ToArray());

    public Task<IReadOnlyList<Subcategory>> ListSubcategoriesAsync(CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<Subcategory>>(_subcategories.ToArray());

    public Task<Category?> FindCategoryAsync(Guid id, CancellationToken cancellationToken) =>
        Task.FromResult(_categories.SingleOrDefault(item => item.Id == id));

    public Task<Subcategory?> FindSubcategoryAsync(Guid id, CancellationToken cancellationToken) =>
        Task.FromResult(_subcategories.SingleOrDefault(item => item.Id == id));

    public void Add(Category category) => _categories.Add(category);

    public void Add(Subcategory subcategory) => _subcategories.Add(subcategory);

    public Task SaveChangesAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
