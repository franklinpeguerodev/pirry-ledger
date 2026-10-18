using PirryLedger.Core.AccessControl.Domain.Entities;
using PirryLedger.Core.AccessControl.Domain.Enums;

namespace PirryLedger.Core.AccessControl.Application;

public interface IRecoveryCodeRepository
{
    Task<RecoveryCode?> FindByHashAsync(string codeHash, CancellationToken cancellationToken = default);

    Task InvalidatePendingAsync(Guid userId, CancellationToken cancellationToken = default);

    Task SaveAsync(RecoveryCode code, CancellationToken cancellationToken = default);
}
