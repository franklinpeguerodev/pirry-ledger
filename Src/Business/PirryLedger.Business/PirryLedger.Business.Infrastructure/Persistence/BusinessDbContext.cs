using Microsoft.EntityFrameworkCore;
using PirryLedger.Business.Domain.Entities;
using PirryLedger.Business.Infrastructure.EntityConfigurations;

namespace PirryLedger.Business.Infrastructure.Persistence;

public sealed class BusinessDbContext : DbContext
{
    public BusinessDbContext(DbContextOptions<BusinessDbContext> options)
        : base(options)
    {
    }

    public DbSet<Invoice> Invoices => Set<Invoice>();

    public DbSet<InvoiceItem> InvoiceItems => Set<InvoiceItem>();

    public DbSet<InvoicePayment> InvoicePayments => Set<InvoicePayment>();

    public DbSet<InvoiceEdit> InvoiceEdits => Set<InvoiceEdit>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(BusinessDbContext).Assembly);
    }
}
