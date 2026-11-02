namespace PirryLedger.Core.Contracts.Permissions;

// ADR 012: resultado de pedir un permiso en nombre del usuario. Created dice
// que la solicitud se guardo; AlreadyPending, que ya habia una Pending para el
// mismo usuario y permiso y no se creo otra.
public enum PermissionRequestOutcome
{
    Created,
    AlreadyPending,
}
