using PirryLedger.Core.AccessControl.Application;
using PirryLedger.Core.AccessControl.Domain.Entities;
using PirryLedger.Core.AccessControl.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace PirryLedger.Core.AccessControl.Infrastructure;

// context propio de la pieza de AccessControl, separado del de notificaciones:
// cada pieza es duena de sus tablas y no ve las de la otra. Las dos comparten
// servidor, no esquema (RD-01).
//
// Es Public porque el Host lo resuelve por inyeccion de dependencias. Lo que no
// hace el Host es tocarlo: solo lo registra.
public sealed class AccessControlDbContext : DbContext
{
    public AccessControlDbContext(DbContextOptions<AccessControlDbContext> options)
        : base(options)
    {
    }

    public DbSet<User> Users => Set<User>();

    public DbSet<ActivationToken> ActivationTokens => Set<ActivationToken>();

    public DbSet<RecoveryCode> RecoveryCodes => Set<RecoveryCode>();

    public DbSet<Session> Sessions => Set<Session>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AccessControlDbContext).Assembly);
    }
}
