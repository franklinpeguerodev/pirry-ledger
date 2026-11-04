using System.ComponentModel.DataAnnotations;

namespace PirryLedger.Core.AccessControl.Api;

// RF-CA-01 y docs/adr/013-admin-created-users.md: lo que llega por el cuerpo del
// alta. Es un record aparte de la entidad: la entidad nunca se enlaza
// directamente a una peticion (RD-02).
//
// NO tiene propiedad Role, y esa ausencia es la decision, no un olvido.
// System.Text.Json ignora los miembros desconocidos, asi que un cliente que
// envie "role": "Administrator" obtiene un Standard igual. La unica puerta a
// Administrator sigue siendo RF-CA-08 (POST /api/admin/users/{id}/role). La
// prueba UnRolEnviadoEnElCuerpoSeIgnoraYElUsuarioNaceEstandar lo fija.
//
// Los atributos Required de DataAnnotations son la primera barrera, pero no la
// unica: el caso de uso vuelve a validar Email, contrasena y Name. Un endpoint
// mal construido a mano pasa por los dos (RD-07), y por eso los mensajes de
// error los produce el caso de uso, no los atributos.
public sealed record CreateUserRequest(
    [property: Required(ErrorMessage = "El Name es obligatorio.")]
    string Name,

    [property: Required(ErrorMessage = "El Email es obligatorio.")]
    string Email,

    [property: Required(ErrorMessage = "La contrasena es obligatoria.")]
    string Password,

    [property: Required(ErrorMessage = "La sucursal es obligatoria.")]
    Guid BranchId);
