using Microsoft.EntityFrameworkCore;
using PirryLedger.Core.Notifications.Domain;

namespace PirryLedger.Core.Notifications.Infrastructure;

// La pieza de notificaciones tiene su propio contexto y sus propias tablas. El
// contexto de control de acceso no puede ver CorreoEnCola y este no puede ver
// Usuario: cada modulo es el unico dueño de sus datos.
//
// El contexto se registra aqui y no en el Host, que solo llama a
// AddNotifications (RD-01).
public sealed class NotificationsDbContext : DbContext
{
    public NotificationsDbContext(DbContextOptions<NotificationsDbContext> options)
        : base(options)
    {
    }

    public DbSet<CorreoEnCola> CorreosEnCola => Set<CorreoEnCola>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfiguration(new CorreoEnColaConfiguration());
    }
}
