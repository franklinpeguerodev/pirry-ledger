namespace PirryLedger.Core.AccessControl.Api;

// Respuestas de los endpoints. Un record por respuesta, para que el JSON sea
// explicito y no dependa de como se serialice la entidad (RD-02).
public sealed record ErrorResponse(string Mensaje);

public sealed record ActivadoResponse(bool Activado);

// RF-CA-17. El campo "enviado" es lo que el usuario lee, no lo que ocurrio de
// verdad: si el correo no existe tampoco se mando nada. Por eso la respuesta no
// puede depender de si el correo estaba dado de alta.
public sealed record EnviadoResponse(bool Enviado);

// RF-CA-03. El token viaja una sola vez, en el cuerpo de la respuesta. No se
// guarda en una cookie ni se devuelve en ninguna otra respuesta.
public sealed record TokenResponse(string Token);

// RF-CA-07. Lo unico que se devuelve del usuario autenticado: nombre, correo y
// rol. Nunca el hash de la contrasena, nunca el token y nunca
// CredencialVersion.
public sealed record YoResponse(string Nombre, string Correo, string Rol);

// RF-CA-08. El rol llega como texto, no como numero del enum: si llegara el
// numero, un cliente con una version distinta del API interpretaria 0 y 1 de
// otra manera, y el JSON dejaria de ser legible en un log.
public sealed record ChangeRoleRequest(string Rol);