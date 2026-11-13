using PirryLedger.Business.Domain.Rules;

namespace PirryLedger.Business.Domain.Entities;

public sealed class Category
{
    public Guid Id { get; private set; }
    public string Name { get; private set; } = string.Empty;

    public static Category Create(string name)
    {
        var category = new Category { Id = Guid.NewGuid() };
        category.Rename(name);
        return category;
    }

    public void Rename(string name) => Name = ValidateName(name);

    internal static string ValidateName(string name)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Trim().Length > Product.MaximumNameLength)
        {
            throw new InvoiceRuleViolationException(
                $"El nombre de la clasificación debe tener entre 1 y {Product.MaximumNameLength} caracteres.");
        }

        return name.Trim();
    }
}
