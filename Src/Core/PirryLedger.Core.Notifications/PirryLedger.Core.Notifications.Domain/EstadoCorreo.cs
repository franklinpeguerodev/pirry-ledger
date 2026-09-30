namespace PirryLedger.Core.Notifications.Domain;

// Estado del correo en la cola. El estado fallido y los reintentos llegan en la
// semana 11, asi que todavia no existe: se anota aqui para que la semana 11 solo
// tenga que agregar el valor y su logica, sin cambiar la tabla.
public enum EstadoCorreo
{
    // Encolado por la operacion que lo origino. Es el unico estado en el que
    // el emisor puede tomar el correo.
    Pendiente = 0,

    // Tomado por el emisor y en manos del transporte SMTP. Existe para que el
    // reclamo sea atomico (RF-NOT-12): si el proceso muere aqui, el correo no
    // se vuelve a marcar Pendiente ni se pierde.
    Procesando = 1,

    // Aceptado por el servidor de correo. No se vuelve a enviar nunca mas.
    Enviado = 2,
}
