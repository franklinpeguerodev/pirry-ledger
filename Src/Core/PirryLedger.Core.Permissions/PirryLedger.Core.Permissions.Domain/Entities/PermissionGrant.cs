namespace PirryLedger.Core.Permissions.Domain.Entities;

// RF-GP-05 + ADR 008: vista operativa de "que permiso tiene cada usuario ahora".
// Una fila por (user_id, permission_name) concedido. La revocacion (futura)
// borra la fila sin tocar la solicitud original.
//
// El INSERT ocurre dentro de la transaccion de ApprovePermissionRequest, en
// el siguiente PR. Aqui solo esta la entidad y su estado.
public sealed class PermissionGrant
{
    private PermissionGrant()
    {
    }

    private PermissionGrant(
        Guid id,
        Guid userId,
        string permissionName,
        DateTime grantedAtUtc,
        Guid? grantedByUserId)
    {
        Id = id;
        UserId = userId;
        PermissionName = permissionName;
        GrantedAtUtc = grantedAtUtc;
        GrantedByUserId = grantedByUserId;
    }

    public Guid Id { get; private set; }

    public Guid UserId { get; private set; }

    public string PermissionName { get; private set; } = string.Empty;

    public DateTime GrantedAtUtc { get; private set; }

    // Quien concedio (el aprobador de la solicitud). Nullable por si en el
    // futuro un permiso se concede sin solicitud previa (seed o admin directo).
    public Guid? GrantedByUserId { get; private set; }

    public static PermissionGrant Create(
        Guid userId,
        string permissionName,
        DateTime grantedAtUtc,
        Guid? grantedByUserId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(permissionName);

        return new PermissionGrant(
            Guid.NewGuid(),
            userId,
            permissionName,
            grantedAtUtc,
            grantedByUserId);
    }

    // RF-GP-05: si el par (user_id, permission_name) ya existe, una nueva
    // aprobacion re-asigna los sellos de tiempo y autor sin crear una fila
    // nueva. Asi se respeta el indice unico
    // ux_per_permission_grants_user_permission (ADR 008) y la semantica "el
    // permiso sigue concedido a partir de esta aprobacion". Llamado por el
    // repositorio cuando detecta la fila existente antes de hacer el INSERT.
    public void Regrant(DateTime grantedAtUtc, Guid? grantedByUserId)
    {
        GrantedAtUtc = grantedAtUtc;
        GrantedByUserId = grantedByUserId;
    }
}