namespace PirryLedger.Core.Contracts.Time;

// RD-11: todas las fechas y horas del sistema pasan por aqui, en UTC. Ningun
// modulo llama a DateTime.UtcNow directamente, para que las piezas que registran
// el mismo instante muestren la misma hora.
public interface IClock
{
    DateTime UtcNow { get; }
}
