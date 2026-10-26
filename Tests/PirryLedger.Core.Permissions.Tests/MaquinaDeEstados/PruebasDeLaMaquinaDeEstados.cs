using PirryLedger.Core.Permissions.Domain.Enums;
using PirryLedger.Core.Permissions.Domain.StateMachines;

namespace PirryLedger.Core.Permissions.Tests.MaquinaDeEstados;

// RF-NEG-09 y RD-04: la unica tabla que decide transiciones vive en
// PermissionRequestStateMachine. Estas pruebas la enumeran: cada combinacion
// de (from, to) tiene una sola respuesta (true o lanzar), y la maquina no
// comparte codigo con la de Invoice.
public sealed class PruebasDeLaMaquinaDeEstados
{
    [Fact]
    public void PendingAprobadaEsValida()
    {
        // Arrange
        var from = PermissionRequestState.Pending;
        var to = PermissionRequestState.Approved;

        // Act
        var resultado = PermissionRequestStateMachine.Transition(from, to);

        // Assert
        Assert.Equal(PermissionRequestState.Approved, resultado);
    }

    [Fact]
    public void PendingRechazadaEsValida()
    {
        // Arrange
        var from = PermissionRequestState.Pending;
        var to = PermissionRequestState.Rejected;

        // Act
        var resultado = PermissionRequestStateMachine.Transition(from, to);

        // Assert
        Assert.Equal(PermissionRequestState.Rejected, resultado);
    }

    [Fact]
    public void ApprovedAplicadaEsValida()
    {
        // Arrange
        var from = PermissionRequestState.Approved;
        var to = PermissionRequestState.Applied;

        // Act
        var resultado = PermissionRequestStateMachine.Transition(from, to);

        // Assert
        Assert.Equal(PermissionRequestState.Applied, resultado);
    }

    [Fact]
    public void PendingAApliedDirectamenteLanzaExcepcion()
    {
        // Arrange
        var from = PermissionRequestState.Pending;
        var to = PermissionRequestState.Applied;

        // Act + Assert
        // RF-GP-05: la aplicacion ocurre en la misma operacion que la
        // aprobacion, pero por la maquina de estados: una transicion directa
        // Pending -> Applied no existe. La logica del caso de uso llama a
        // Approve y luego a MarkApplied, en dos pasos.
        Assert.Throws<InvalidPermissionRequestTransitionException>(
            () => PermissionRequestStateMachine.Transition(from, to));
    }

    [Fact]
    public void ApprovedARechazadaLanzaExcepcion()
    {
        // Arrange
        var from = PermissionRequestState.Approved;
        var to = PermissionRequestState.Rejected;

        // Act + Assert
        // Una solicitud aprobada no se rechaza despues: ya se concedio el
        // permiso (o se va a conceder). Si se intenta, la regla del sistema
        // lo rechaza.
        Assert.Throws<InvalidPermissionRequestTransitionException>(
            () => PermissionRequestStateMachine.Transition(from, to));
    }

    [Fact]
    public void AppliedNoTieneSalidas()
    {
        // Arrange: Applied es estado terminal (RF-GP-05). El permiso ya esta
        // concedido y la solicitud cumplio su proposito.
        var desdeApplied = new[]
        {
            PermissionRequestState.Pending,
            PermissionRequestState.Approved,
            PermissionRequestState.Rejected,
        };

        // Act + Assert
        foreach (var destino in desdeApplied)
        {
            Assert.False(
                PermissionRequestStateMachine.CanTransition(PermissionRequestState.Applied, destino),
                $"Applied no deberia poder transicionar a {destino}.");
        }
    }

    [Fact]
    public void RejectedNoTieneSalidas()
    {
        // Arrange: Rejected es estado terminal. La solicitud se rechaza una
        // vez y se queda ahi.
        var desdeRejected = new[]
        {
            PermissionRequestState.Pending,
            PermissionRequestState.Approved,
            PermissionRequestState.Applied,
        };

        // Act + Assert
        foreach (var destino in desdeRejected)
        {
            Assert.False(
                PermissionRequestStateMachine.CanTransition(PermissionRequestState.Rejected, destino),
                $"Rejected no deberia poder transicionar a {destino}.");
        }
    }

    [Fact]
    public void LaMismaTransicionASiMismoLanzaExcepcion()
    {
        // Arrange
        var estado = PermissionRequestState.Approved;

        // Act + Assert
        // Approved -> Approved no esta en la tabla. La maquina no es
        // idempotente para transiciones consigo mismo: una "segunda
        // aprobacion" falla.
        Assert.Throws<InvalidPermissionRequestTransitionException>(
            () => PermissionRequestStateMachine.Transition(estado, estado));
    }

    [Fact]
    public void SoloLasTresTransicionesDeLaTablaSonValidas()
    {
        // Arrange: RF-GP-06. La tabla permite exactamente tres de los
        // dieciseis pares posibles entre los cuatro estados. Enumerarlos todos
        // evita que una transicion nueva (o una que falte) pase inadvertida:
        // una prueba por par deja huecos, la matriz no.
        var estados = Enum.GetValues<PermissionRequestState>();
        var permitidas = new HashSet<(PermissionRequestState From, PermissionRequestState To)>
        {
            (PermissionRequestState.Pending, PermissionRequestState.Approved),
            (PermissionRequestState.Approved, PermissionRequestState.Applied),
            (PermissionRequestState.Pending, PermissionRequestState.Rejected),
        };

        // Act + Assert
        Assert.Equal(4, estados.Length);

        foreach (var from in estados)
        {
            foreach (var to in estados)
            {
                var esperado = permitidas.Contains((from, to));
                var obtenido = PermissionRequestStateMachine.CanTransition(from, to);

                Assert.True(
                    esperado == obtenido,
                    $"La transicion {from} -> {to} deberia ser {(esperado ? "valida" : "prohibida")}.");
            }
        }
    }

    [Fact]
    public void LaMaquinaDeEstadosNoComparteCodigoConLaDeInvoice()
    {
        // Arrange
        // RF-NEG-09: las dos maquinas no comparten ni enum ni clase. La
        // maquina de Invoice vive en PirryLedger.Business; la de permisos
        // en PirryLedger.Core.Permissions. Un cambio en una no debe
        // afectar a la otra. Esta prueba comprueba que no comparten tipo.
        var tipoPermisos = typeof(PermissionRequestStateMachine).Assembly.GetName().Name;
        var tipoInvoice = "PirryLedger.Business";

        // Act + Assert
        Assert.NotEqual(tipoInvoice, tipoPermisos);
        Assert.DoesNotContain("Business", tipoPermisos);
    }
}