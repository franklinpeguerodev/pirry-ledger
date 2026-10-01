namespace PirryLedger.Core.AccessControl.Infrastructure;

// Parametros de Argon2id, configurables para que una prueba pueda usar valores
// bajos y no tardar.
//
// Los valores por defecto son los que exige OWASP como minimo (m=19456 KiB,
// t=2, p=1) y estan anotados con su fuente en docs/adr/. El valor real no se
// toma de una variable de entorno: subir o bajar los parametros cambia la
// seguridad, y esa decision se toma en el codigo, no por configuracion.
public sealed record ParametrosDeArgon2(
    int MemoriaKb = 19456,
    int Iteraciones = 2,
    int Paralelismo = 1,
    int LongitudDeLaSalEnBytes = 16,
    int LongitudDelHashEnBytes = 32)
{
    public void Validar()
    {
        if (Iteraciones < 1)
        {
            throw new ArgumentOutOfRangeException(
                nameof(Iteraciones), "Argon2id necesita al menos una iteracion.");
        }

        if (Paralelismo < 1)
        {
            throw new ArgumentOutOfRangeException(
                nameof(Paralelismo), "Argon2id necesita al menos un hilo.");
        }

        // El algoritmo reserva ocho bloques por hilo, por debajo de eso falla.
        if (MemoriaKb < 8 * Paralelismo)
        {
            throw new ArgumentOutOfRangeException(
                nameof(MemoriaKb),
                $"Argon2id necesita al menos {8 * Paralelismo} KiB para {Paralelismo} hilo(s).");
        }

        if (LongitudDeLaSalEnBytes < 16)
        {
            throw new ArgumentOutOfRangeException(
                nameof(LongitudDeLaSalEnBytes), "La sal necesita al menos 16 bytes.");
        }

        if (LongitudDelHashEnBytes < 16)
        {
            throw new ArgumentOutOfRangeException(
                nameof(LongitudDelHashEnBytes), "El hash necesita al menos 16 bytes.");
        }
    }
}
