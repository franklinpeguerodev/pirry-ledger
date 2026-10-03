using PirryLedger.Core.AccessControl.Application;
using PirryLedger.Core.AccessControl.Domain;
using PirryLedger.Core.Contracts.Notifications;
using Xunit;

namespace PirryLedger.Core.AccessControl.Tests.Credencial;

public sealed class PruebasDeRecuperacionDeContrasena
{
    [Fact]
    public async Task SolicitudDesconocidaYConocidaRespondenIgual()
    {
        var reloj = new RelojFalso(new DateTime(2026, 10, 1, 12, 0, 0, DateTimeKind.Utc));
        var usuarios = new UsuarioEnMemoria();
        var cola = new ColaEnMemoria();
        var caso = new PasswordRecovery(
            usuarios,
            new CodigosEnMemoria(),
            cola,
            reloj,
            new HasherDePrueba(),
            TimeSpan.FromMinutes(15));

        await caso.RequestAsync("no-existe@ejemplo.com");
        usuarios.Crear("Ana", "ana@ejemplo.com", "old", reloj.UtcNow, activo: true);
        await caso.RequestAsync("ana@ejemplo.com");

        Assert.Single(cola.Correos);
        Assert.Contains("15 minutos", cola.Correos[0].Cuerpo);
    }

    [Fact]
    public async Task CodigoEsDeUnSoloUsoYExpiraEnQuinceMinutos()
    {
        var reloj = new RelojFalso(new DateTime(2026, 10, 1, 12, 0, 0, DateTimeKind.Utc));
        var usuarios = new UsuarioEnMemoria();
        var usuario = usuarios.Crear("Ana", "ana@ejemplo.com", "old", reloj.UtcNow, activo: true);
        var codigos = new CodigosEnMemoria();
        var cola = new ColaEnMemoria();
        var caso = new PasswordRecovery(usuarios, codigos, cola, reloj, new HasherDePrueba(), TimeSpan.FromMinutes(15));

        await caso.RequestAsync(usuario.Correo);
        var codigo = cola.Correos[0].Cuerpo.Split('\n', StringSplitOptions.TrimEntries)
            .First(line => line.Length == OneTimeCodeGenerator.LongitudEnBytes * 2);

        await caso.CompleteAsync(codigo, "new12345");
        await Assert.ThrowsAsync<RecoveryRejectedException>(() => caso.CompleteAsync(codigo, "other123"));

        reloj.Avanzar(TimeSpan.FromMinutes(15));
        await Assert.ThrowsAsync<RecoveryRejectedException>(() => caso.CompleteAsync(codigo, "other123"));
    }

    private sealed class ColaEnMemoria : IEmailQueue
    {
        public List<QueuedEmail> Correos { get; } = [];

        public Task EnqueueAsync(QueuedEmail email, CancellationToken cancellationToken = default)
        {
            Correos.Add(email);
            return Task.CompletedTask;
        }
    }

    private sealed class CodigosEnMemoria : IRecoveryCodeRepository
    {
        private readonly List<CodigoRecuperacion> _codes = [];

        public Task<CodigoRecuperacion?> BuscarPorHashAsync(string hashDelCodigo, CancellationToken cancellationToken = default) =>
            Task.FromResult(_codes.FirstOrDefault(code => code.HashDelCodigo == hashDelCodigo));

        public Task InvalidarPendientesAsync(Guid usuarioId, CancellationToken cancellationToken = default)
        {
            _codes.RemoveAll(code => code.UsuarioId == usuarioId && !code.EstaUsado);
            return Task.CompletedTask;
        }

        public Task GuardarAsync(CodigoRecuperacion codigo, CancellationToken cancellationToken = default)
        {
            _codes.RemoveAll(existing => existing.Id == codigo.Id);
            _codes.Add(codigo);
            return Task.CompletedTask;
        }
    }

    private sealed class HasherDePrueba : IPasswordHasher
    {
        public string Hash(string contrasena) => $"hash:{contrasena}";

        public bool Verificar(string contrasena, string hashGuardado) => hashGuardado == Hash(contrasena);

        public string CrearHashSenaluelo() => "dummy";
    }
}
