using Microsoft.EntityFrameworkCore;
using PirryLedger.Core.Notifications.Application;
using PirryLedger.Core.Notifications.Domain;

namespace PirryLedger.Core.Notifications.Infrastructure;

internal sealed class CorreoEnColaRepository : ICorreoEnColaRepository
{
    private readonly NotificationsDbContext _contexto;

    public CorreoEnColaRepository(NotificationsDbContext contexto)
    {
        _contexto = contexto;
    }

    // RF-NOT-12. El reclamo es una sola sentencia: UPDATE con un subquery que
    // pide filas con FOR UPDATE SKIP LOCKED. Si dos procesos ejecutan el emisor
    // a la vez, el primero marca sus filas como Procesando y el segundo, al
    // mirar, ya no las ve como Pendiente. Por eso el segundo no puede duplicar
    // ningun envio, y por eso el estado cambia en la base y no despues en
    // memoria.
    public async Task<IReadOnlyList<CorreoEnCola>> ReclamarPendientesAsync(int limite, CancellationToken cancellationToken = default)
    {
        var correos = await _contexto.CorreosEnCola
            .FromSqlInterpolated($$"""
                UPDATE not_correos_en_cola
                SET estado = {{(int)EstadoCorreo.Procesando}},
                    intentos = intentos + 1
                WHERE id IN (
                    SELECT id
                    FROM not_correos_en_cola
                    WHERE estado = {{(int)EstadoCorreo.Pendiente}}
                    ORDER BY fecha_creacion_utc
                    FOR UPDATE SKIP LOCKED
                    LIMIT {{limite}}
                )
                RETURNING *;
                """)
            .ToListAsync(cancellationToken);

        return correos;
    }

    public Task GuardarAsync(CancellationToken cancellationToken = default)
    {
        return _contexto.SaveChangesAsync(cancellationToken);
    }

    public async Task<int> ContarPendientesAsync(CancellationToken cancellationToken = default)
    {
        return await _contexto.CorreosEnCola
            .CountAsync(correo => correo.Estado == EstadoCorreo.Pendiente, cancellationToken);
    }
}
