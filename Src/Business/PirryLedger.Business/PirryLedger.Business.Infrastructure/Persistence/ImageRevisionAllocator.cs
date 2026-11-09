using Microsoft.EntityFrameworkCore;

namespace PirryLedger.Business.Infrastructure.Persistence;

// La reserva de revisiones del conjunto de imagenes (ADR 015).
//
// Cada cambio del conjunto necesita un numero de revision global, y ese
// numero tiene que salir en la MISMA transaccion que el cambio: si el
// contador subiera en una transaccion aparte, el cliente podria avanzar su
// cursor por encima de un cambio que todavia no se ha escrito, y ese cambio
// no le llegaria nunca.
//
// El UPDATE toma el bloqueo de la fila del contador y lo mantiene hasta que
// se confirme la transaccion. Con ese bloqueo, las escrituras quedan
// serializadas y la revision coincide con el orden de confirmacion.
public static class ImageRevisionAllocator
{
    public static async Task<long> ReserveAsync(
        BusinessDbContext context,
        CancellationToken cancellationToken = default)
    {
        if (context.Database.IsRelational())
        {
            await context.Database.ExecuteSqlRawAsync(
                "UPDATE bil_image_revision SET value = value + 1 WHERE id = 1",
                cancellationToken);

            return await context.ImageRevisionCounters
                .AsNoTracking()
                .Select(counter => counter.Value)
                .SingleAsync(cancellationToken);
        }

        // El proveedor en memoria que usan las pruebas no entiende SQL ni
        // bloqueos: ahi el numero se lleva en la entidad y se guarda con el
        // siguiente SaveChanges, que ocurre en la misma transaccion.
        var counter = await context.ImageRevisionCounters.SingleAsync(cancellationToken);
        counter.Value += 1;

        return counter.Value;
    }
}
