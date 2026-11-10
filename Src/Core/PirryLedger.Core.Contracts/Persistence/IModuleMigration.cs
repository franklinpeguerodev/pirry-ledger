namespace PirryLedger.Core.Contracts.Persistence;

// ADR 018: cada pieza aplica sus propias migraciones detras de este puerto.
//
// El Host solo recorre los pasos registrados y para en el primero que falla: no
// conoce el DbContext de ninguna pieza. Asi, una pieza nueva se suma
// registrando su paso y el Host no se toca.
//
// El orden es el de registro. No hay claves foraneas entre piezas, asi que el
// orden no cambia el resultado, pero lo deja determinista y legible.
public interface IModuleMigration
{
    // Nombre de la pieza, para el mensaje de consola. Nunca un dato sensible.
    string Name { get; }

    Task ApplyAsync(CancellationToken cancellationToken);
}
