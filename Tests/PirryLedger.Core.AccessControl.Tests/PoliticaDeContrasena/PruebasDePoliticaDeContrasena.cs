using PirryLedger.Core.AccessControl.Application;

namespace PirryLedger.Core.AccessControl.Tests.PoliticaDeContrasena;

// RF-CA-14: politica minima de contrasena, al menos 8 caracteres, con letras y
// numeros. El criterio dice "en el registro y en todo cambio de contrasena",
// asi que el unico sitio que decide es PasswordPolicy.Evaluate, y lo llaman
// todos los casos de uso.
//
// Estas pruebas fijan la politica por su cuenta, sin pasar por un endpoint:
// la longitud minima (el borde exacto y un caracter por debajo), y la exigencia
// de que haya letras y de que haya numeros. Cada prueba verifica un solo
// comportamiento, con sus tres fases Arrange / Act / Assert y sin estado
// compartido entre ellas.
public sealed class PruebasDePoliticaDeContrasena
{
    [Fact]
    public void ContrasenaValidaCumpleLaPolitica()
    {
        // Arrange: contrasena con longitud mayor que el minimo, con letras y
        // con numeros. Cubre el camino feliz.
        const string contrasena = "abc12345";

        // Act
        var resultado = PasswordPolicy.Evaluate(contrasena);

        // Assert
        Assert.True(resultado.IsValid);
        Assert.Null(resultado.Reason);
    }

    [Fact]
    public void ContrasenaDeExactamenteOchoCaracteresConLetrasYNumerosEsValida()
    {
        // Arrange: borde inferior de longitud. Si la regla fuera
        // "password.Length <= MinimumLength" en vez de "<", esta contrasena
        // seria rechazada y la prueba falla. La inclusion del limite es parte
        // del criterio de aceptacion.
        const string contrasena = "abcd1234";

        // Act
        var resultado = PasswordPolicy.Evaluate(contrasena);

        // Assert
        Assert.True(resultado.IsValid);
        Assert.Null(resultado.Reason);
    }

    [Fact]
    public void ContrasenaConSieteCaracteresSeRechazaPorLongitud()
    {
        // Arrange: un caracter por debajo del minimo, con letras y numeros. Si
        // la regla fuera "password.Length > MinimumLength" en vez de "<", esta
        // contrasena seria aceptada y la prueba falla.
        const string contrasena = "abc1234";

        // Act
        var resultado = PasswordPolicy.Evaluate(contrasena);

        // Assert
        Assert.False(resultado.IsValid);
        Assert.NotNull(resultado.Reason);
        Assert.Contains(PasswordPolicy.MinimumLength.ToString(), resultado.Reason);
    }

    [Fact]
    public void ContrasenaSinNumerosSeRechaza()
    {
        // Arrange: ocho caracteres, con letras y sin ningun numero. Cumple la
        // longitud pero no la segunda mitad del criterio.
        const string contrasena = "abcdefgh";

        // Act
        var resultado = PasswordPolicy.Evaluate(contrasena);

        // Assert
        Assert.False(resultado.IsValid);
        Assert.NotNull(resultado.Reason);
    }

    [Fact]
    public void ContrasenaSinLetrasSeRechaza()
    {
        // Arrange: ocho caracteres, con numeros y sin ninguna letra. El caso
        // simetrico del anterior.
        const string contrasena = "12345678";

        // Act
        var resultado = PasswordPolicy.Evaluate(contrasena);

        // Assert
        Assert.False(resultado.IsValid);
        Assert.NotNull(resultado.Reason);
    }

    [Fact]
    public void ContrasenaVaciaSeRechaza()
    {
        // Arrange: sin contrasena. El mensaje dice que es obligatoria, que es
        // distinto del de longitud.
        const string contrasena = "";

        // Act
        var resultado = PasswordPolicy.Evaluate(contrasena);

        // Assert
        Assert.False(resultado.IsValid);
        Assert.NotNull(resultado.Reason);
    }
}
