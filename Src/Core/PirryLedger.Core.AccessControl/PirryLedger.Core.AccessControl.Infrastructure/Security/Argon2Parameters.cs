using PirryLedger.Core.AccessControl.Application;
using PirryLedger.Core.AccessControl.Domain.Entities;
using PirryLedger.Core.AccessControl.Domain.Enums;

namespace PirryLedger.Core.AccessControl.Infrastructure;

// Parametros de Argon2id, configurables para que una prueba pueda usar valores
// bajos y no tardar.
//
// Los valores por defecto son los que exige OWASP como minimo (m=19456 KiB,
// t=2, p=1) y estan anotados con su fuente en docs/adr/. El valor real no se
// toma de una variable de entorno: subir o bajar los parametros cambia la
// seguridad, y esa decision se toma en el codigo, no por configuracion.
public sealed record Argon2Parameters(
    int MemoryKb = 19456,
    int Iterations = 2,
    int Parallelism = 1,
    int SaltLengthInBytes = 16,
    int HashLengthInBytes = 32)
{
    public void Validate()
    {
        if (Iterations < 1)
        {
            throw new ArgumentOutOfRangeException(
                nameof(Iterations), "Argon2id necesita al menos una iteracion.");
        }

        if (Parallelism < 1)
        {
            throw new ArgumentOutOfRangeException(
                nameof(Parallelism), "Argon2id necesita al menos un hilo.");
        }

        // El algoritmo reserva ocho bloques por hilo, por debajo de eso falla.
        if (MemoryKb < 8 * Parallelism)
        {
            throw new ArgumentOutOfRangeException(
                nameof(MemoryKb),
                $"Argon2id necesita al menos {8 * Parallelism} KiB para {Parallelism} hilo(s).");
        }

        if (SaltLengthInBytes < 16)
        {
            throw new ArgumentOutOfRangeException(
                nameof(SaltLengthInBytes), "La sal necesita al menos 16 bytes.");
        }

        if (HashLengthInBytes < 16)
        {
            throw new ArgumentOutOfRangeException(
                nameof(HashLengthInBytes), "El hash necesita al menos 16 bytes.");
        }
    }
}
