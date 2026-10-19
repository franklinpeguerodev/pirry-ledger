namespace PirryLedger.Core.Contracts.Notifications;

// Un Email todavia no enviado. El cuerpo lo arma el modulo que origina el
// Email y el modulo de notificaciones solo se encarga de entregarlos.
public sealed record QueuedEmail(string Recipient, string Subject, string Body);
