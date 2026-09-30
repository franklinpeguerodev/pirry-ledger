namespace PirryLedger.Core.Contracts.Notifications;

// Un correo todavia no enviado. El cuerpo lo arma el modulo que origina el
// correo y el modulo de notificaciones solo se encarga de entregarlos.
public sealed record QueuedEmail(string Destinatario, string Asunto, string Cuerpo);
