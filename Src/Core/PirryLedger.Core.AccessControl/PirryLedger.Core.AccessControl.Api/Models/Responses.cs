using PirryLedger.Core.AccessControl.Application;
using PirryLedger.Core.AccessControl.Domain.Entities;
using PirryLedger.Core.AccessControl.Domain.Enums;

namespace PirryLedger.Core.AccessControl.Api;

// Respuestas de los endpoints. Un record por respuesta, para que el JSON sea
// explicito y no dependa de como se serialice la entidad (RD-02).
public sealed record ErrorResponse(string Message);

public sealed record ActivatedResponse(bool Activated);

// RF-CA-17. El campo "enviado" es lo que el User lee, no lo que ocurrio de
// verdad: si el Email no existe tampoco se mando nada. Por eso la respuesta no
// puede depender de si el Email estaba dado de alta.
public sealed record SentResponse(bool Sent);

// RF-CA-03. El token viaja una sola vez, en el cuerpo de la respuesta. No se
// guarda en una cookie ni se devuelve en ninguna otra respuesta.
public sealed record TokenResponse(string Token);

// RF-CA-07. Lo unico que se devuelve del User autenticado: Name, Email y
// Role. Nunca el hash de la contrasena, nunca el token y nunca
// CredentialVersion.
public sealed record MeResponse(string Name, string Email, string Role);

// RF-CA-08. El role llega como texto, no como numero del enum: si llegara el
// numero, un cliente con una version distinta del API interpretaria 0 y 1 de
// otra manera, y el JSON dejaria de ser legible en un log.
public sealed record ChangeRoleRequest(string Role);
