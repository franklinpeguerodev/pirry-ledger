using Microsoft.EntityFrameworkCore;
using PirryLedger.Business.Application.Ports;
using PirryLedger.Business.Domain.Entities;

namespace PirryLedger.Business.Infrastructure.Persistence;

public sealed class ClassificationRepository(BusinessDbContext context) : IClassificationRepository
{
    public async Task<IReadOnlyList<Category>> ListCategoriesAsync(CancellationToken cancellationToken) =>
        await context.Set<Category>().AsNoTracking().OrderBy(item => item.Name).ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<Subcategory>> ListSubcategoriesAsync(CancellationToken cancellationToken) =>
        await context.Set<Subcategory>().AsNoTracking().OrderBy(item => item.Name).ToListAsync(cancellationToken);

    public Task<Category?> FindCategoryAsync(Guid id, CancellationToken cancellationToken) =>
        context.Set<Category>().SingleOrDefaultAsync(item => item.Id == id, cancellationToken);

    public Task<Subcategory?> FindSubcategoryAsync(Guid id, CancellationToken cancellationToken) =>
        context.Set<Subcategory>().SingleOrDefaultAsync(item => item.Id == id, cancellationToken);

    public void Add(Category category) => context.Add(category);

    public void Add(Subcategory subcategory) => context.Add(subcategory);

    public Task SaveChangesAsync(CancellationToken cancellationToken) => context.SaveChangesAsync(cancellationToken);
}
