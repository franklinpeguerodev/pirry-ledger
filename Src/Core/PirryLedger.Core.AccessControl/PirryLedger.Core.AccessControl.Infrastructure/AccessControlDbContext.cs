using Microsoft.EntityFrameworkCore;
using PirryLedger.Core.AccessControl.Domain;

namespace PirryLedger.Core.AccessControl.Infrastructure;

// Contexto propio de la pieza de AccessControl, separado del de notificaciones:
// cada pieza es duena de sus tablas y no ve las de la otra. Las dos comparten
// servidor, no esquema (RD-01).
//
// Es publico porque el Host lo resuelve por inyeccion de dependencias. Lo que no
// hace el Host es tocarlo: solo lo registra.
public sealed class AccessControlDbContext : DbContext
{
    public AccessControlDbContext(DbContextOptions<AccessControlDbContext> opciones)
        : base(opciones)
    {
    }

    public DbSet<Usuario> Usuarios => Set<Usuario>();

    public DbSet<TokenActivacion> TokensDeActivacion => Set<TokenActivacion>();

    public DbSet<CodigoRecuperacion> CodigosDeRecuperacion => Set<CodigoRecuperacion>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AccessControlDbContext).Assembly);
    }
}