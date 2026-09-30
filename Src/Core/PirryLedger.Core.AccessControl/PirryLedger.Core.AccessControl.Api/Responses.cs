namespace PirryLedger.Core.AccessControl.Api;

// Respuestas de los endpoints. Un record por respuesta, para que el JSON sea
// explicito y no dependa de como se serialice la entidad (RD-02).
public sealed record ErrorResponse(string Mensaje);

public sealed record ActivadoResponse(bool Activado);

// RF-CA-17. El campo "enviado" es lo que el usuario lee, no lo que ocurrio de
// verdad: si el correo no existe tampoco se mando nada. Por eso la respuesta no
// puede depender de si el correo estaba dado de alta.
public sealed record EnviadoResponse(bool Enviado);