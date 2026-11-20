using PirryLedger.Business.Application.Ports;
using PirryLedger.Business.Domain.Entities;
using PirryLedger.Business.Domain.Enums;
using PirryLedger.Core.Contracts.Time;

namespace PirryLedger.Business.Application.UseCases;

// RF-NEG-02: abono protegido por sucursal y transacción en producción.
// La sobrecarga de dos dependencias mantiene las pruebas aisladas existentes.
public sealed class RegisterDebtPayment
{
    private readonly IInvoiceRepository _invoices;
    private readonly IClock _clock;
    private readonly ManageDebtPayments? _manager;

    public RegisterDebtPayment(IInvoiceRepository invoices, IClock clock)
    {
        _invoices = invoices;
        _clock = clock;
    }

    public RegisterDebtPayment(IInvoiceRepository invoices, IClock clock, ManageDebtPayments manager)
        : this(invoices, clock)
    {
        _manager = manager;
    }

    public async Task<Invoice> ExecuteAsync(
        Guid invoiceId,
        decimal amount,
        Guid registeredByUserId,
        CancellationToken cancellationToken = default,
        PaymentMethod method = PaymentMethod.Cash)
    {
        if (_manager is not null)
        {
            return await _manager.RegisterAsync(invoiceId, amount, method, cancellationToken);
        }

        var invoice = await _invoices.GetRequiredAsync(invoiceId, cancellationToken);

        invoice.RegisterDebtPayment(amount, _clock.UtcNow, registeredByUserId, method);

        await _invoices.SaveChangesAsync(cancellationToken);

        return invoice;
    }
}
