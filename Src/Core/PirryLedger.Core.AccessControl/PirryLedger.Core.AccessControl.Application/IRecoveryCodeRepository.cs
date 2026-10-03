using PirryLedger.Core.AccessControl.Domain;

namespace PirryLedger.Core.AccessControl.Application;

public interface IRecoveryCodeRepository
{
    Task<CodigoRecuperacion?> BuscarPorHashAsync(string hashDelCodigo, CancellationToken cancellationToken = default);

    Task InvalidarPendientesAsync(Guid usuarioId, CancellationToken cancellationToken = default);

    Task GuardarAsync(CodigoRecuperacion codigo, CancellationToken cancellationToken = default);
}
