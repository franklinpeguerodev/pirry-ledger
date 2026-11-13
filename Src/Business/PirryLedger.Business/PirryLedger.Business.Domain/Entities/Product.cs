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

    public Guid? SubcategoryId { get; private set; }

    public void AssignSubcategory(Guid? subcategoryId) => SubcategoryId = subcategoryId;

    public bool IsActive { get; private set; }

    // La revision global en la que el producto cambio por ultima vez de
    // activo a inactivo o al reves (ADR 015).
    //
    // Sirve para que el cliente reciba la imagen cuando el producto se
    // reactiva y la borre cuando se desactiva, sin tocar la imagen misma: la
    // foto de un producto desactivado sigue en el bucket, porque reactivarlo
    // la necesita.
    public long ImageRevision { get; private set; }

    // El producto cambio de estado y su imagen forma parte del conjunto. El
    // numero lo reserva el repositorio, dentro de la misma transaccion que
    // guarda el cambio.
    public void MarkImageSetChanged(long revision)
    {
        if (revision <= 0)
        {
            throw new InvoiceRuleViolationException("La revision debe ser mayor que cero.");
        }

        ImageRevision = revision;
    }

    public static Product Create(string name, string? description, decimal price)
    {
        var normalizedName = NormalizeName(name);
        var normalizedDescription = NormalizeDescription(description);
        ValidatePrice(price);

        return new Product(Guid.NewGuid(), normalizedName, normalizedDescription, price);
    }

    public void Update(string name, string? description, decimal price)
    {
        var normalizedName = NormalizeName(name);
        var normalizedDescription = NormalizeDescription(description);
        ValidatePrice(price);
        Name = normalizedName;
        Description = normalizedDescription;
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

    private static string NormalizeDescription(string? description)
    {
        if (string.IsNullOrWhiteSpace(description))
        {
            throw new InvoiceRuleViolationException("La descripción del producto es obligatoria.");
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
