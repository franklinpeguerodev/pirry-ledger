namespace PirryLedger.Business.Domain.Entities;

public sealed class Subcategory
{
    public Guid Id { get; private set; }
    public Guid CategoryId { get; private set; }
    public string Name { get; private set; } = string.Empty;

    public static Subcategory Create(Guid categoryId, string name)
    {
        var subcategory = new Subcategory { Id = Guid.NewGuid(), CategoryId = categoryId };
        subcategory.Rename(name);
        return subcategory;
    }

    public void Rename(string name) => Name = Category.ValidateName(name);
}
