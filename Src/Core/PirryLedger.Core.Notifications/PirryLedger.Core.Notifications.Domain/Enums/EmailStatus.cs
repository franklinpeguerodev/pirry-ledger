namespace PirryLedger.Core.Notifications.Domain.Enums;

// Status del Email en la cola. El Status Failed y los reintentos llegan en la
// semana 11, asi que todavia no existe: se anota aqui para que la semana 11 solo
// tenga que agregar el valor y su logica, sin cambiar la tabla.
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
}
