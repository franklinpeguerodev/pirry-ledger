using Microsoft.EntityFrameworkCore;
using PirryLedger.Core.Permissions.Domain.Entities;
using PirryLedger.Core.Permissions.Infrastructure.EntityConfigurations;

namespace PirryLedger.Core.Permissions.Infrastructure.Persistence;

// Contexto propio del modulo de Permissions. Cada pieza del Core es duenia de
// sus tablas: el modulo de Permissions no ve las tablas de AccessControl
// (ac_*) ni las de Notifications (not_*); las otras piezas no ven las
// per_*. El vinculo entre modulos ocurre por Interfaces en
// PirryLedger.Core.Contracts (IPermissionAuthority, IUserRepository), no por
// relaciones directas entre tablas (RD-01).
public sealed class PermissionsDbContext : DbContext
{
    public PermissionsDbContext(DbContextOptions<PermissionsDbContext> options)
        : base(options)
    {
    }

    public DbSet<PermissionRequest> PermissionRequests => Set<PermissionRequest>();

    public DbSet<PermissionGrant> PermissionGrants => Set<PermissionGrant>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(PermissionsDbContext).Assembly);
    }
}