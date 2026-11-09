namespace PirryLedger.Business.Application.Ports;

// El resultado de un cambio del conjunto de imagenes: la revision que se
// reservo y la clave de la imagen que salio, si hubo alguna.
//
// La clave que sale es la que hay que borrar del bucket despues de confirmar
// el cambio. Es null cuando no habia imagen previa.
public sealed record ImageSetChange(long Revision, string? RetiredImageKey);
