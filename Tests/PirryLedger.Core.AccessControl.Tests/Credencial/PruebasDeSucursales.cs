using PirryLedger.Core.AccessControl.Domain.Entities;
using PirryLedger.Core.AccessControl.Domain.Enums;

namespace PirryLedger.Core.AccessControl.Tests.Credencial;

public sealed class PruebasDeSucursales
{
    [Fact]
    public void AsignarSucursalAUnStandardInvalidaLasSesiones()
    {
        var user = User.Create(
            "Empleado",
            "empleado@ejemplo.com",
            "hash",
            DateTime.UtcNow);
        user.Activate();

        var credentialVersionBefore = user.CredentialVersion;
        var branchId = Guid.NewGuid();

        user.AssignBranch(branchId);

        Assert.Equal(branchId, user.BranchId);
        Assert.Equal(credentialVersionBefore + 1, user.CredentialVersion);
        Assert.Equal(Role.Standard, user.Role);
    }

    [Fact]
    public void StandardNoPuedeRecibirLaSucursalGlobal()
    {
        var user = User.Create(
            "Empleado",
            "empleado@ejemplo.com",
            "hash",
            DateTime.UtcNow);

        var error = Assert.Throws<ArgumentException>(() => user.AssignBranch(Branch.AllId));

        Assert.Equal("El usuario Estándar debe tener una sucursal física. (Parameter 'branchId')", error.Message);
        Assert.Equal(Branch.AllId, user.BranchId);
    }
}
