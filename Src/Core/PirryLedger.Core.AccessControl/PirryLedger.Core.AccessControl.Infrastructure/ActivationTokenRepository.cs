using Microsoft.EntityFrameworkCore;
using PirryLedger.Core.AccessControl.Application;
using PirryLedger.Core.AccessControl.Domain;

namespace PirryLedger.Core.AccessControl.Infrastructure;

internal sealed class ActivationTokenRepository : IActivationTokenRepository
{
    private readonly AccessControlDbContext _contexto;

    public ActivationTokenRepository(AccessControlDbContext contexto)
    {
        _contexto = contexto;
    }

    // Devuelve el token tanto si esta usado como si caduco. El caso de uso decide
    // que rechazar, y todos esos rechazos son iguales para quien llama
    // (RF-CA-16): filtrar aqui por vigencia dejaria ver si un enlace existio.
    public async Task<TokenActivacion?> BuscarPorHashAsync(
        string hashDelToken,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(hashDelToken))
        {
            return null;
        }

        var normalizado = hashDelToken.Trim().ToLowerInvariant();

        return await _contexto.TokensDeActivacion
            .FirstOrDefaultAsync(token => token.HashDelToken == normalizado, cancellationToken);
    }

    public async Task InvalidarPendientesAsync(
        Guid usuarioId,
        CancellationToken cancellationToken = default)
    {
        // Solo los no usados. Un token ya abierto se conserva: es el registro de que
        // la cuenta quedo activa, y ademas ese correo ya no sirve para nada.
        await _contexto.TokensDeActivacion
            .Where(token => token.UsuarioId == usuarioId && token.UsadoUtc == null)
            .ExecuteDeleteAsync(cancellationToken);
    }

    public async Task GuardarAsync(TokenActivacion token, CancellationToken cancellationToken = default)
    {
        if (_contexto.TokensDeActivacion.Local.Any(registrado => registrado.Id == token.Id))
        {
            _contexto.TokensDeActivacion.Update(token);
        }
        else
        {
            _contexto.TokensDeActivacion.Add(token);
        }

        await _contexto.SaveChangesAsync(cancellationToken);
    }
}