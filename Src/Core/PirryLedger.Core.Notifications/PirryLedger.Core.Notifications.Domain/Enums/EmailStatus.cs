namespace PirryLedger.Core.Notifications.Domain.Enums;

// Estados persistidos de la cola. Cancelled corresponde a correos notificables
// retirados por preferencia o elegibilidad. Límite de intentos e incertidumbre
// se implementan en la siguiente feature del plan, conservando estos ordinales.
public enum EmailStatus
{
    // Encolado por la operacion que lo origino. Es el unico Status en el que
    // el emisor puede tomar el Email.
    Pending = 0,

    // Tomado por el emisor y en manos del transporte SMTP. Existe para que el
    // reclamo sea atomico (RF-NOT-12): si el proceso muere aqui, el Email no
    // se vuelve a marcar Pending ni se pierde.
    Processing = 1,

    // Aceptado por el servidor de Email. No se vuelve a enviar nunca mas.
    Sent = 2,
    Cancelled = 3,
}
