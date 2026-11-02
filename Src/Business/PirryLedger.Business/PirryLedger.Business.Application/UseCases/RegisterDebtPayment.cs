using PirryLedger.Business.Application.Ports;
using PirryLedger.Business.Domain.Entities;
using PirryLedger.Core.Contracts.Time;

namespace PirryLedger.Business.Application.UseCases;

// RF-NEG-02: el Administrador registra un abono de deuda. La regla de estado y
// de saldo vive en el agregado; la fecha y el usuario salen del reloj y de la
// sesion, nunca de la peticion.
public sealed class RegisterDebtPayment
{
    private readonly IInvoiceRepository _invoices;
    private readonly IClock _clock;

    public RegisterDebtPayment(IInvoiceRepository invoices, IClock clock)
    {
        _invoices = invoices;
        _clock = clock;
    }

    public async Task<Invoice> ExecuteAsync(
        Guid invoiceId,
        decimal amount,
        Guid registeredByUserId,
        CancellationToken cancellationToken = default)
    {
        var invoice = await _invoices.GetRequiredAsync(invoiceId, cancellationToken);

        invoice.RegisterDebtPayment(amount, _clock.UtcNow, registeredByUserId);

        await _invoices.SaveChangesAsync(cancellationToken);

        return invoice;
    }
}
