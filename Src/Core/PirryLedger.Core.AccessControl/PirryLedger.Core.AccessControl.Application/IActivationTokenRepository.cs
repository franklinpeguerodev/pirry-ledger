using PirryLedger.Core.AccessControl.Domain;

namespace PirryLedger.Core.AccessControl.Application;

// Tokens de un solo uso. Application no sabe como se guardan, solo que un token
// se busca por el hash de su valor en claro y se marca usado una sola vez.
//
// BuscarPorHashAsync tiene que devolver el token vigente y expirado, porque la
// respuesta para "usado", "vencido" e "inexistente" es la misma y no puede
// distinguir al que llama (RF-CA-16 y RF-CA-09).
public interface IActivationTokenRepository
{
    Task<TokenActivacion?> BuscarPorHashAsync(string hashDelToken, CancellationToken cancellationToken = default);

    // RF-CA-17: al reenviar el enlace, los tokens que el usuario todavia no abrio
    // dejan de valer y se borran.
    //
    // Decision de Franklin: borrar en vez de marcar. La invalidacion no es un
    // "uso" (el token no se abrio nunca) y la auditoria de RF-CA-08 es de la
    // semana 14, fuera de alcance. Asi UsadoUtc sigue significando exactamente
    // una cosa: que alguien pulso el enlace. Un token borrado se comporta igual
    // que uno que nunca existio, que es justo lo que pide la respuesta identica.
    Task InvalidarPendientesAsync(Guid usuarioId, CancellationToken cancellationToken = default);

    Task GuardarAsync(TokenActivacion token, CancellationToken cancellationToken = default);
}