namespace PirryLedger.Core.AccessControl.Domain.Entities;

public sealed class Branch
{
    public static readonly Guid AllId = new("00000000-0000-0000-0000-000000000001");

    private Branch()
    {
    }

    private Branch(Guid id, string name, string address, bool isAll)
    {
        Id = id;
        Name = name;
        Address = address;
        IsAll = isAll;
        Active = true;
    }

    public Guid Id { get; private set; }

    public string Name { get; private set; } = string.Empty;

    public string Address { get; private set; } = string.Empty;

    public bool IsAll { get; private set; }

    public bool Active { get; private set; }

    public static Branch Create(string name, string address)
    {
        Validate(name, address);
        return new Branch(Guid.NewGuid(), name.Trim(), address.Trim(), false);
    }

    public static Branch CreateAll() =>
        new(AllId, "Todas", "Todas las sucursales", true);

    public void Update(string name, string address)
    {
        if (IsAll)
        {
            throw new InvalidOperationException("La sucursal global no se puede modificar.");
        }

        Validate(name, address);
        Name = name.Trim();
        Address = address.Trim();
    }

    public void SetActive(bool active)
    {
        if (IsAll && !active)
        {
            throw new InvalidOperationException("La sucursal global no se puede desactivar.");
        }

        Active = active;
    }

    private static void Validate(string name, string address)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Trim().Length > 120)
        {
            throw new ArgumentException("El nombre de la sucursal es obligatorio y no puede superar 120 caracteres.", nameof(name));
        }

        if (string.IsNullOrWhiteSpace(address) || address.Trim().Length > 100)
        {
            throw new ArgumentException("La direccion es obligatoria y no puede superar 100 caracteres.", nameof(address));
        }
    }
}
