using PirryLedger.Core.AccessControl.Application;
using PirryLedger.Core.AccessControl.Domain.Enums;
using PirryLedger.Core.Documents.Application.Exceptions;
using PirryLedger.Core.Documents.Domain.Entities;

namespace PirryLedger.Core.Documents.Application;

// RF-DOC-03 + RF-DOC-04: la regla de propiedad, en un solo sitio.
//
// No se puede expresar en RoleRequirements porque no depende del rol sino de
// quien es el duenio del documento concreto. Por eso se compara por identidad,
// como RF-GP-07, y no se deja al endpoint (RD-06).
internal static class DocumentOwnership
{
    public static void EnsureCanAccess(AuthenticatedUser caller, Document document)
    {
        if (document.IsOwnedBy(caller.Id) || caller.Role == Role.Administrator)
        {
            return;
        }

        throw new DocumentAccessDeniedException();
    }
}
