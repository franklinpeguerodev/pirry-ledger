using PirryLedger.Core.AccessControl.Domain.Entities;
using PirryLedger.Core.AccessControl.Domain.Enums;
using PirryLedger.Core.Contracts.Auth;
using PirryLedger.Core.Contracts.Permissions;
using PirryLedger.Core.Contracts.Time;

namespace PirryLedger.Core.AccessControl.Application;

public sealed class RecoveryRejectedException : Exception
{
    public RecoveryRejectedException() : base("El codigo de recuperacion no es valido.")
    {
    }
}

// RF-CA-09, RF-CA-10 y RF-CA-11, y docs/adr/014-password-change-permission.md.
//
// El flujo de recuperacion cambio en ADR 014 y el cambio importa:
//
//   Standard -> pedir la recuperacion NO genera codigo ni envia correo. Crea una
//               solicitud ResetPassword Pending. El codigo se emite cuando un
//               Administrator la aprueba (ApprovePermissionRequest llama a
//               IPasswordResetIssuer). Asi "codigo valido" implica "aprobacion
//               concedida" y RF-CA-11 se cumple tal como esta escrito.
//
//   Administrator -> excepcion 9: recupera su cuenta con solo su buzon, igual
//               que antes. Si exigiera aprobacion, con un solo Administrator
//               RF-GP-07 haria imposible aprobar su propia solicitud y el
//               sistema quedaria encerrado.
//
// La respuesta de RequestAsync es la misma exista o no el correo, y en los dos
// caminos: el endpoint siempre responde 202. Que el Standard no reciba nada
// todavia no se revela en la API; lo explica el README.
public sealed class PasswordRecovery
{
    private readonly IUserRepository _users;
    private readonly IRecoveryCodeRepository _codes;
    private readonly IPasswordResetIssuer _issuer;
    private readonly IPermissionAuthority _permissions;
    private readonly IPermissionRequestGateway _requests;
    private readonly IClock _clock;
    private readonly IPasswordHasher _hasher;

    public PasswordRecovery(
        IUserRepository users,
        IRecoveryCodeRepository codes,
        IPasswordResetIssuer issuer,
        IPermissionAuthority permissions,
        IPermissionRequestGateway requests,
        IClock clock,
        IPasswordHasher hasher)
    {
        _users = users;
        _codes = codes;
        _issuer = issuer;
        _permissions = permissions;
        _requests = requests;
        _clock = clock;
        _hasher = hasher;
    }

    public async Task RequestAsync(string? email, CancellationToken cancellationToken = default)
    {
        if (!EmailValidator.Evaluate(email).IsValid)
        {
            return;
        }

        var user = await _users.FindByEmailAsync(email!.Trim(), cancellationToken);
        if (user is null)
        {
            return;
        }

        // ADR 014, decision 6: el Administrator esta exento. Su codigo se emite
        // y se envia ahora, como antes.
        if (user.Role == Role.Administrator)
        {
            await _issuer.IssueAsync(user.Id, cancellationToken);
            return;
        }

        // ADR 014, decision 2: el Standard no recibe codigo hasta que un
        // Administrator apruebe. Si ya hay una Pending, no se crea otra
        // (PermissionRequestGateway deduplica) y la respuesta no cambia.
        await _requests.RequestAsync(user.Id, PermissionNames.ResetPassword, cancellationToken);
    }

    public async Task CompleteAsync(
        string? plainCode,
        string? newPassword,
        CancellationToken cancellationToken = default)
    {
        var policy = PasswordPolicy.Evaluate(newPassword);
        if (!policy.IsValid)
        {
            throw new RegistrationRejectedException(policy.Reason!);
        }

        var code = await FindValidCodeAsync(plainCode, cancellationToken);
        var user = code is null
            ? null
            : await _users.FindByIdAsync(code.UserId, cancellationToken);
        if (code is null || user is null)
        {
            throw new RecoveryRejectedException();
        }

        // ADR 014: el Standard consume la concesion ResetPassword. Si no la
        // tiene, el codigo no sirve: el codigo solo se emitio porque un
        // Administrator aprobo, asi que no tener la concesion significa que se
        // uso ya o que algo no cuadra. El Administrator esta exento.
        //
        // Se consume antes de cambiar la contrasena, igual que ADR 011 con la
        // anulacion de facturas: evita que la concesion se use dos veces, y el
        // precio es que un fallo de escritura obliga a repetir el tramite.
        if (user.Role == Role.Standard
            && !await _permissions.ConsumeAsync(user.Id, PermissionNames.ResetPassword, cancellationToken))
        {
            throw new RecoveryRejectedException();
        }

        user.ChangePassword(_hasher.Hash(newPassword!), _clock.UtcNow);
        code.MarkUsed(_clock.UtcNow);
        await _users.SaveAsync(user, cancellationToken);
        await _codes.SaveAsync(code, cancellationToken);
    }

    private async Task<RecoveryCode?> FindValidCodeAsync(
        string? plainCode,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(plainCode))
        {
            return null;
        }

        var code = await _codes.FindByHashAsync(
            OneTimeCodeGenerator.CalculateHash(plainCode.Trim()),
            cancellationToken);
        return code is null || code.IsUsed || code.HasExpired(_clock.UtcNow) ? null : code;
    }
}
