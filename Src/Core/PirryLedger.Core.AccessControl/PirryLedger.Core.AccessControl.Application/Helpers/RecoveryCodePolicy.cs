namespace PirryLedger.Core.AccessControl.Application;

// ADR 014, decision 7: el codigo de recuperacion vale cuatro horas.
//
// La duracion vive aqui y no en el registro de dependencias para que el texto
// del correo y la fecha de vencimiento no puedan decir cosas distintas. Antes de
// ADR 014 eran quince minutos y el cuerpo del correo lo repetia escrito a mano;
// con la aprobacion del Administrador en medio, el reloj empieza a contar cuando
// se emite el codigo, y el usuario puede no estar mirando el correo en ese
// momento.
//
// El codigo sigue siendo de un solo uso y se guarda solo como SHA-256.
public static class RecoveryCodePolicy
{
    public static readonly TimeSpan Validity = TimeSpan.FromHours(4);

    // Lo que el usuario lee en el correo. Se deriva de la duracion que se usa de
    // verdad, para que un cambio no deje el mensaje mintiendo.
    public static string Describe(TimeSpan validity)
    {
        if (validity.TotalHours >= 1)
        {
            var hours = (int)validity.TotalHours;

            return hours == 1 ? "1 hora" : $"{hours} horas";
        }

        var minutes = (int)validity.TotalMinutes;

        return minutes == 1 ? "1 minuto" : $"{minutes} minutos";
    }
}
