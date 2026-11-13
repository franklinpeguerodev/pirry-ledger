using PirryLedger.Business.Application.Ports;
using PirryLedger.Business.Domain.Entities;
using PirryLedger.Business.Domain.Rules;

namespace PirryLedger.Business.Application.UseCases;

public sealed class ManageClassification(IClassificationRepository repository)
{
    public async Task<IReadOnlyList<CategoryItem>> ListAsync(CancellationToken cancellationToken)
    {
        var categories = await repository.ListCategoriesAsync(cancellationToken);
        var subcategories = await repository.ListSubcategoriesAsync(cancellationToken);
        var byCategory = subcategories.ToLookup(item => item.CategoryId);
        return categories.Select(category => new CategoryItem(
            category.Id, category.Name, byCategory[category.Id].Select(SubcategoryItem.From).ToArray())).ToArray();
    }

    public async Task<CategoryItem> SaveCategoryAsync(Guid? id, string name, CancellationToken cancellationToken)
    {
        var category = id is Guid existingId
            ? await repository.FindCategoryAsync(existingId, cancellationToken)
                ?? throw new InvoiceRuleViolationException("La categoría no existe.")
            : Category.Create(name);

        if (id.HasValue)
            category.Rename(name);
        else
            repository.Add(category);

        await repository.SaveChangesAsync(cancellationToken);
        var subcategories = await repository.ListSubcategoriesAsync(cancellationToken);
        return new CategoryItem(category.Id, category.Name, subcategories
            .Where(item => item.CategoryId == category.Id).Select(SubcategoryItem.From).ToArray());
    }

    public async Task<SubcategoryItem> SaveSubcategoryAsync(
        Guid categoryId, Guid? id, string name, CancellationToken cancellationToken)
    {
        if (await repository.FindCategoryAsync(categoryId, cancellationToken) is null)
            throw new InvoiceRuleViolationException("La categoría no existe.");

        var subcategory = id is Guid existingId
            ? await repository.FindSubcategoryAsync(existingId, cancellationToken)
                ?? throw new InvoiceRuleViolationException("La subcategoría no existe.")
            : Subcategory.Create(categoryId, name);

        if (subcategory.CategoryId != categoryId)
            throw new InvoiceRuleViolationException("La subcategoría no pertenece a la categoría.");

        if (id.HasValue)
            subcategory.Rename(name);
        else
            repository.Add(subcategory);

        await repository.SaveChangesAsync(cancellationToken);
        return SubcategoryItem.From(subcategory);
    }
}

public sealed record CategoryItem(Guid Id, string Name, IReadOnlyList<SubcategoryItem> Subcategories);

public sealed record SubcategoryItem(Guid Id, Guid CategoryId, string Name)
{
    public static SubcategoryItem From(Subcategory item) => new(item.Id, item.CategoryId, item.Name);
}
