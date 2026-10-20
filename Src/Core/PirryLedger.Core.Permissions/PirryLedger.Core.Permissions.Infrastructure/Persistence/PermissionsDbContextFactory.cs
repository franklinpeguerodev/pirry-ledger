using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace PirryLedger.Core.Permissions.Infrastructure.Persistence;

// Fabrica en tiempo de diseno para que `dotnet ef migrations add` pueda
// instanciar el contexto sin levantar el Host. Solo se usa desde la
// herramienta de linea de comandos; el Host resuelve el contexto por DI.
public sealed class PermissionsDbContextFactory : IDesignTimeDbContextFactory<PermissionsDbContext>
{
    public PermissionsDbContext CreateDbContext(string[] args)
    {
        // La cadena de conexion real la lee el Host del .env al arrancar. En
        // tiempo de diseno basta una cadena con la misma forma para que EF
        // pueda leer el modelo.
        var options = new DbContextOptionsBuilder<PermissionsDbContext>()
            .UseNpgsql("Host=localhost;Database=pirry_ledger;Username=postgres;Password=postgres")
            .Options;

        return new PermissionsDbContext(options);
    }
}