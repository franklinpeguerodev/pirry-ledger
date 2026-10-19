using PirryLedger.Core.Notifications.Application;
using PirryLedger.Core.Notifications.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using PirryLedger.Core.Notifications.Domain.Entities;

namespace PirryLedger.Core.Notifications.Infrastructure;

// La pieza de notificaciones tiene su propio context y sus propias tablas. El
// context de control de acceso no puede ver EmailQueueItem y este no puede ver
// User: cada modulo es el unico dueño de sus datos.
//
// El context se registra aqui y no en el Host, que solo llama a
// AddNotifications (RD-01).
public sealed class NotificationsDbContext : DbContext
{
    public NotificationsDbContext(DbContextOptions<NotificationsDbContext> options)
        : base(options)
    {
    }

    public DbSet<EmailQueueItem> EmailQueueItems => Set<EmailQueueItem>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfiguration(new EmailQueueItemConfiguration());
    }
}
