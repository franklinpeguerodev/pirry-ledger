using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Org.BouncyCastle.Crypto.Generators;
using Org.BouncyCastle.Crypto.Parameters;
using PirryLedger.Core.AccessControl.Application;

namespace PirryLedger.Core.AccessControl.Infrastructure;

// Unico lugar del sistema que produce y compara hashes de contrasena.
//
// Formato del hash, versionado en un solo valor (formato PHC):
//
//   $argon2id$v=19$m=<memoria KiB>,t=<iteraciones>,p=<hilos>$<sal>$<hash>
//
// con sal y hash en Base64 sin relleno. Meter los parametros dentro del valor
// permite subirlos o cambiar de algoritmo mas adelante sin romper los hashes ya
// guardados: un hash viejo trae sus propios parametros y se verifica con ellos.
public sealed class Argon2idPasswordHasher : IPasswordHasher
{
    private const string Prefijo = "$argon2id$";
    private const string Version = "v=19";

    private readonly ParametrosDeArgon2 _parametros;

    public Argon2idPasswordHasher(ParametrosDeArgon2 parametros)
    {
        parametros.Validar();
        _parametros = parametros;
    }

    public string Hash(string contrasena)
    {
        ArgumentException.ThrowIfNullOrEmpty(contrasena);

        // Sal de 16 bytes del generador del sistema, distinta para cada usuario y
        // para cada cambio de contrasena (RF-CA-02).
        var sal = RandomNumberGenerator.GetBytes(_parametros.LongitudDeLaSalEnBytes);

        return ConstruirHash(contrasena, sal, _parametros);
    }

    public bool Verificar(string contrasena, string hashGuardado)
    {
        if (string.IsNullOrEmpty(contrasena) || string.IsNullOrEmpty(hashGuardado))
        {
            return false;
        }

        if (!IntentarLeer(hashGuardado, out var sal, out var hashEsperado, out var parametros))
        {
            return false;
        }

        var calculado = Derivar(contrasena, sal, parametros, hashEsperado.Length);

        // Tiempo constante: la comparacion no puede revelar cuantos bytes
        // acertaron antes de fallar. Nunca con ==.
        return CryptographicOperations.FixedTimeEquals(calculado, hashEsperado);
    }

    public string CrearHashSenaluelo()
    {
        return Hash(Convert.ToHexString(RandomNumberGenerator.GetBytes(32)));
    }

    private static string ConstruirHash(string contrasena, byte[] sal, ParametrosDeArgon2 parametros)
    {
        var hash = Derivar(contrasena, sal, parametros, parametros.LongitudDelHashEnBytes);

        var parametros_texto = string.Create(
            CultureInfo.InvariantCulture,
            $"m={parametros.MemoriaKb},t={parametros.Iteraciones},p={parametros.Paralelismo}");

        return string.Concat(
            Prefijo,
            Version,
            "$",
            parametros_texto,
            "$",
            Base64SinRelleno(sal),
            "$",
            Base64SinRelleno(hash));
    }

    private static byte[] Derivar(string contrasena, byte[] sal, ParametrosDeArgon2 parametros, int longitudDelHash)
    {
        var argumentos = new Argon2Parameters.Builder(Argon2Parameters.Argon2id)
            .WithSalt(sal)
            .WithMemoryAsKB(parametros.MemoriaKb)
            .WithIterations(parametros.Iteraciones)
            .WithParallelism(parametros.Paralelismo)
            .WithVersion(Argon2Parameters.Version13)
            .Build();

        var generador = new Argon2BytesGenerator();
        generador.Init(argumentos);

        var hash = new byte[longitudDelHash];
        generador.GenerateBytes(Encoding.UTF8.GetBytes(contrasena), hash);

        return hash;
    }

    // Un hash con prefijo, version o parametros que no conocemos se trata como
    // "no coincide". Nunca como excepcion: un dato corrupto en la base no puede
    // convertirse en un error con traza hacia el usuario (RD-08).
    private static bool IntentarLeer(
        string hashGuardado,
        out byte[] sal,
        out byte[] hashEsperado,
        out ParametrosDeArgon2 parametros)
    {
        sal = [];
        hashEsperado = [];
        parametros = new ParametrosDeArgon2();

        if (!hashGuardado.StartsWith(Prefijo, StringComparison.Ordinal))
        {
            return false;
        }

        var partes = hashGuardado.Split('$', StringSplitOptions.None);

        // ["", "argon2id", "v=19", "m=..,t=..,p=..", sal, hash]
        if (partes.Length != 6 || !string.Equals(partes[2], Version, StringComparison.Ordinal))
        {
            return false;
        }

        if (!IntentarLeerParametros(partes[3], out parametros))
        {
            return false;
        }

        try
        {
            sal = FromBase64SinRelleno(partes[4]);
            hashEsperado = FromBase64SinRelleno(partes[5]);
        }
        catch (FormatException)
        {
            return false;
        }

        if (sal.Length == 0 || hashEsperado.Length == 0)
        {
            return false;
        }

        return true;
    }

    private static bool IntentarLeerParametros(string texto, out ParametrosDeArgon2 parametros)
    {
        parametros = new ParametrosDeArgon2();

        var parsed = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var par in texto.Split(',', StringSplitOptions.RemoveEmptyEntries))
        {
            var separador = par.IndexOf('=');

            if (separador <= 0)
            {
                return false;
            }

            parsed[par[..separador]] = par[(separador + 1)..];
        }

        if (!parsed.TryGetValue("m", out var memoria) ||
            !parsed.TryGetValue("t", out var iteraciones) ||
            !parsed.TryGetValue("p", out var paralelismo))
        {
            return false;
        }

        if (!int.TryParse(memoria, NumberStyles.None, CultureInfo.InvariantCulture, out var memoriaKb) ||
            !int.TryParse(iteraciones, NumberStyles.None, CultureInfo.InvariantCulture, out var vueltas) ||
            !int.TryParse(paralelismo, NumberStyles.None, CultureInfo.InvariantCulture, out var hilos))
        {
            return false;
        }

        parametros = new ParametrosDeArgon2(
            MemoriaKb: memoriaKb,
            Iteraciones: vueltas,
            Paralelismo: hilos,
            LongitudDeLaSalEnBytes: 16,
            LongitudDelHashEnBytes: 32);

        try
        {
            parametros.Validar();
        }
        catch (ArgumentOutOfRangeException)
        {
            return false;
        }

        return true;
    }

    private static string Base64SinRelleno(byte[] valor) =>
        Convert.ToBase64String(valor).TrimEnd('=');

    private static byte[] FromBase64SinRelleno(string valor)
    {
        var relleno = (4 - (valor.Length % 4)) % 4;

        return Convert.FromBase64String(valor + new string('=', relleno));
    }
}
