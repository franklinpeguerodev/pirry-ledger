namespace PirryLedger.Core.Contracts.Notifications;

// RF-NOT-08: la cola es el unico camino para sacar Email de la aplicacion.
// Quien origina el Email (por ejemplo AccessControl al registrar un User)
// llama a EnqueueAsync y sigue trabajando: nunca abre una conexion SMTP.
//
// La pieza de Notifications implementa este contrato y es la unica que toca la
// tabla de correos en cola. Ningun modulo mas escribe en ella, y el modulo que
// la implementa no conoce a quien le escribe (RD-01).
public interface IEmailQueue
{
    Task EnqueueAsync(QueuedEmail email, CancellationToken cancellationToken = default);
}
