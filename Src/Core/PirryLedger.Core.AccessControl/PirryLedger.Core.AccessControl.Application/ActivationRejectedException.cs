namespace PirryLedger.Core.AccessControl.Application;

// El enlace de activacion no sirvio: ya se uso, caduco o no existe. El mensaje es
// el mismo en los tres casos, para no revelar nada sobre el token (RF-CA-16).
public sealed class ActivationRejectedException : Exception
{
    public ActivationRejectedException()
        : base("El enlace de activacion no es valido o ya caduco.")
    {
    }
}