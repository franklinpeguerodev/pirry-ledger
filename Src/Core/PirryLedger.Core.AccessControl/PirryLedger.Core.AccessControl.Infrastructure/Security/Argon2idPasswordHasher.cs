using PirryLedger.Core.AccessControl.Application;
using PirryLedger.Core.AccessControl.Domain.Entities;
using PirryLedger.Core.AccessControl.Domain.Enums;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Org.BouncyCastle.Crypto.Generators;

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
    private const string Prefix = "$argon2id$";
    private const string Version = "v=19";

    private readonly Argon2Parameters _parameters;

    // El hash senuelo se calcula UNA vez por instancia y se reutiliza.
    //
    // Calcularlo en cada intento cuesta un Argon2 extra: el Email inexistente
    // tardaba el doble que una contrasena erronea (hash + verificacion contra uno,
    // solo verificacion contra el otro). Esa diferencia de tiempo se mide en
    // milisegundos desde fuera y enumera las cuentas igual de bien que un
    // message distinto. Calcularlo una vez deja ambos caminos en una sola
    // verificacion.
    private readonly Lazy<string> _dummyHash;

    public Argon2idPasswordHasher(Argon2Parameters parameters)
    {
        parameters.Validate();
        _parameters = parameters;

        // Random de 32 bytes: una contrasena que nadie conoce, asi que que el
        // mismo hash se reutilice no filtra nada y no se puede exploitear.
        _dummyHash = new Lazy<string>(
            () => Hash(Convert.ToHexString(RandomNumberGenerator.GetBytes(32))),
            LazyThreadSafetyMode.ExecutionAndPublication);
    }

    public string Hash(string password)
    {
        ArgumentException.ThrowIfNullOrEmpty(password);

        // Sal de 16 bytes del generador del sistema, distinta para cada User y
        // para cada cambio de contrasena (RF-CA-02).
        var salt = RandomNumberGenerator.GetBytes(_parameters.SaltLengthInBytes);

        return BuildHash(password, salt, _parameters);
    }

    public bool Verify(string password, string storedHash)
    {
        if (string.IsNullOrEmpty(password) || string.IsNullOrEmpty(storedHash))
        {
            return false;
        }

        if (!TryRead(storedHash, out var salt, out var expectedHash, out var parameters))
        {
            return false;
        }

        var calculated = Derive(password, salt, parameters, expectedHash.Length);

        // Tiempo constante: la comparacion no puede revelar cuantos bytes
        // acertaron antes de fallar. Nunca con ==.
        return CryptographicOperations.FixedTimeEquals(calculated, expectedHash);
    }

    public string CreateDummyHash()
    {
        return _dummyHash.Value;
    }

    private static string BuildHash(string password, byte[] salt, Argon2Parameters parameters)
    {
        var hash = Derive(password, salt, parameters, parameters.HashLengthInBytes);

        var parametersText = string.Create(
            CultureInfo.InvariantCulture,
            $"m={parameters.MemoryKb},t={parameters.Iterations},p={parameters.Parallelism}");

        return string.Concat(
            Prefix,
            Version,
            "$",
            parametersText,
            "$",
            Base64WithoutPadding(salt),
            "$",
            Base64WithoutPadding(hash));
    }

    private static byte[] Derive(string password, byte[] salt, Argon2Parameters parameters, int hashLength)
    {
        var arguments = new Org.BouncyCastle.Crypto.Parameters.Argon2Parameters.Builder(Org.BouncyCastle.Crypto.Parameters.Argon2Parameters.Argon2id)
            .WithSalt(salt)
            .WithMemoryAsKB(parameters.MemoryKb)
            .WithIterations(parameters.Iterations)
            .WithParallelism(parameters.Parallelism)
            .WithVersion(Org.BouncyCastle.Crypto.Parameters.Argon2Parameters.Version13)
            .Build();

        var generator = new Argon2BytesGenerator();
        generator.Init(arguments);

        var hash = new byte[hashLength];
        generator.GenerateBytes(Encoding.UTF8.GetBytes(password), hash);

        return hash;
    }

    // Un hash con prefix, version o parametros que no conocemos se trata como
    // "no coincide". Nunca como excepcion: un dato corrupto en la base no puede
    // convertirse en un error con traza hacia el User (RD-08).
    private static bool TryRead(
        string storedHash,
        out byte[] salt,
        out byte[] expectedHash,
        out Argon2Parameters parameters)
    {
        salt = [];
        expectedHash = [];
        parameters = new Argon2Parameters();

        if (!storedHash.StartsWith(Prefix, StringComparison.Ordinal))
        {
            return false;
        }

        var parts = storedHash.Split('$', StringSplitOptions.None);

        // ["", "argon2id", "v=19", "m=..,t=..,p=..", sal, hash]
        if (parts.Length != 6 || !string.Equals(parts[2], Version, StringComparison.Ordinal))
        {
            return false;
        }

        if (!TryReadParameters(parts[3], out parameters))
        {
            return false;
        }

        try
        {
            salt = FromBase64WithoutPadding(parts[4]);
            expectedHash = FromBase64WithoutPadding(parts[5]);
        }
        catch (FormatException)
        {
            return false;
        }

        if (salt.Length == 0 || expectedHash.Length == 0)
        {
            return false;
        }

        return true;
    }

    private static bool TryReadParameters(string text, out Argon2Parameters parameters)
    {
        parameters = new Argon2Parameters();

        var parsed = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var pair in text.Split(',', StringSplitOptions.RemoveEmptyEntries))
        {
            var separator = pair.IndexOf('=');

            if (separator <= 0)
            {
                return false;
            }

            parsed[pair[..separator]] = pair[(separator + 1)..];
        }

        if (!parsed.TryGetValue("m", out var memory) ||
            !parsed.TryGetValue("t", out var iterations) ||
            !parsed.TryGetValue("p", out var parallelism))
        {
            return false;
        }

        if (!int.TryParse(memory, NumberStyles.None, CultureInfo.InvariantCulture, out var memoryKb) ||
            !int.TryParse(iterations, NumberStyles.None, CultureInfo.InvariantCulture, out var rounds) ||
            !int.TryParse(parallelism, NumberStyles.None, CultureInfo.InvariantCulture, out var threads))
        {
            return false;
        }

        parameters = new Argon2Parameters(
            MemoryKb: memoryKb,
            Iterations: rounds,
            Parallelism: threads,
            SaltLengthInBytes: 16,
            HashLengthInBytes: 32);

        try
        {
            parameters.Validate();
        }
        catch (ArgumentOutOfRangeException)
        {
            return false;
        }

        return true;
    }

    private static string Base64WithoutPadding(byte[] value) =>
        Convert.ToBase64String(value).TrimEnd('=');

    private static byte[] FromBase64WithoutPadding(string value)
    {
        var padding = (4 - (value.Length % 4)) % 4;

        return Convert.FromBase64String(value + new string('=', padding));
    }
}
