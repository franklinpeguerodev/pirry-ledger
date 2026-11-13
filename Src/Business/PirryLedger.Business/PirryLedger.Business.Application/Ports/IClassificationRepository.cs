using PirryLedger.Business.Domain.Entities;

namespace PirryLedger.Business.Application.Ports;

public interface IClassificationRepository
{
    Task<IReadOnlyList<Category>> ListCategoriesAsync(CancellationToken cancellationToken);
    Task<IReadOnlyList<Subcategory>> ListSubcategoriesAsync(CancellationToken cancellationToken);
    Task<Category?> FindCategoryAsync(Guid id, CancellationToken cancellationToken);
    Task<Subcategory?> FindSubcategoryAsync(Guid id, CancellationToken cancellationToken);
    void Add(Category category);
    void Add(Subcategory subcategory);
    Task SaveChangesAsync(CancellationToken cancellationToken);
}
