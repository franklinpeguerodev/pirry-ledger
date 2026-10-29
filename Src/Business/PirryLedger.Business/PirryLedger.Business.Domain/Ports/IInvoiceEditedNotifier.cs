namespace PirryLedger.Business.Domain.Ports;

using PirryLedger.Business.Domain.Entities;

// El aviso al Administrador no se envia dentro de la operacion: se encola y un
// proceso aparte lo manda (regla de seguridad de AGENTS.md). Hoy la
// implementacion es vacia; la real llega en la semana 10 (RF-NEG-07). El
// puerto vive en el dominio para que el agregado pueda notificar sin depender
// de Application ni de Infrastructure.
public interface IInvoiceEditedNotifier
{
    void Notify(Invoice invoice, Guid editedByUserId, DateTime editedAtUtc);
}
