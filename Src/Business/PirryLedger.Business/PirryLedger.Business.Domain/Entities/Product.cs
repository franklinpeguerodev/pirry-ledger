using PirryLedger.Business.Domain.Rules;

namespace PirryLedger.Business.Domain.Entities;

public sealed class Product
{
    public const int MaximumNameLength = 120;
    public const int MaximumDescriptionLength = 500;

    private Product()
    {
    }

    private Product(Guid id, string name, string? description, decimal price)
    {
        Id = id;
        Name = name;
        Description = description;
        Price = price;
        IsActive = true;
    }

    public Guid Id { get; private set; }

    public string Name { get; private set; } = string.Empty;

    public string? Description { get; private set; }

    public decimal Price { get; private set; }

    public bool IsActive { get; private set; }

    public static Product Create(string name, string? description, decimal price)
    {
        var normalizedName = NormalizeName(name);
        var normalizedDescription = NormalizeDescription(description);
        ValidatePrice(price);

        return new Product(Guid.NewGuid(), normalizedName, normalizedDescription, price);
    }

    public void Update(string name, string? description, decimal price)
    {
        Name = NormalizeName(name);
        Description = NormalizeDescription(description);
        ValidatePrice(price);
        Price = price;
    }

    public void SetActive(bool isActive) => IsActive = isActive;

    private static string NormalizeName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new InvoiceRuleViolationException("El nombre del producto es obligatorio.");
        }

        var trimmed = name.Trim();

        if (trimmed.Length > MaximumNameLength)
        {
            throw new InvoiceRuleViolationException(
                $"El nombre del producto no puede pasar de {MaximumNameLength} caracteres.");
        }

        return trimmed;
    }

    private static string? NormalizeDescription(string? description)
    {
        if (string.IsNullOrWhiteSpace(description))
        {
            return null;
        }

        var trimmed = description.Trim();

        if (trimmed.Length > MaximumDescriptionLength)
        {
            throw new InvoiceRuleViolationException(
                $"La descripcion del producto no puede pasar de {MaximumDescriptionLength} caracteres.");
        }

        return trimmed;
    }

    private static void ValidatePrice(decimal price)
    {
        if (!Money.HasExactDecimals(price) || price <= 0m || price > Money.MaximumAmount)
        {
            throw new InvoiceRuleViolationException(
                $"El precio del producto debe ser mayor que cero, tener dos decimales exactos y no pasar de {Money.MaximumAmount:0.00}.");
        }
    }
}
