using PirryLedger.Core.AccessControl.Domain;

namespace PirryLedger.Core.AccessControl.Application;

// Lo unico que Application necesita saber de como se guardan las sesiones.
public interface ISessionRepository
{
    Task GuardarAsync(Sesion sesion, CancellationToken cancellationToken = default);

    // Devuelve la sesion exista o no, sin filtrar por vigencia: el punto de
    // validacion decide que hacer con una caducada, y filtrar aqui permitiria
    // distinguir "esta caducada" de "no existe" (lo mismo que RF-CA-16 con el
    // token de activacion).
    Task<Sesion?> BuscarPorHashAsync(string hashDelToken, CancellationToken cancellationToken = default);

    // RF-CA-18: cerrar una sesion sin conocer el usuario devuelve false si el
    // token no existe o ya estaba cerrado, para que el endpoint responda siempre
    // igual.
    Task<bool> CerrarPorHashAsync(string hashDelToken, DateTime ahoraUtc, CancellationToken cancellationToken = default);
}