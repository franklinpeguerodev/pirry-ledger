using PirryLedger.Core.AccessControl.Domain.Entities;
using PirryLedger.Core.AccessControl.Domain.Enums;

namespace PirryLedger.Core.AccessControl.Application;

// Lo unico que Application necesita saber de como se guardan las sesiones.
public interface ISessionRepository
{
    Task SaveAsync(Session session, CancellationToken cancellationToken = default);

    // Devuelve la Session exista o no, sin filtrar por vigencia: el punto de
    // validacion decide que hacer con una caducada, y filtrar aqui permitiria
    // distinguir "esta caducada" de "no existe" (lo mismo que RF-CA-16 con el
    // token de activacion).
    Task<Session?> FindByHashAsync(string tokenHash, CancellationToken cancellationToken = default);

    // RF-CA-18: Close una Session sin conocer el User devuelve false si el
    // token no existe o ya estaba cerrado, para que el endpoint responda siempre
    // igual.
    Task<bool> CloseByHashAsync(string tokenHash, DateTime utcNow, CancellationToken cancellationToken = default);
}
