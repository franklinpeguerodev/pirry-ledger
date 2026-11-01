using System.Runtime.CompilerServices;

// La pieza de Permissions expone sus implementaciones de EF Core como internal
// (mismo criterio que PirryLedger.Core.AccessControl.Infrastructure). El
// proyecto de pruebas necesita verlas para poder ejercitar el repositorio real
// sobre un contexto en memoria: el comportamiento de upsert de
// PermissionGrantRepository vive en codigo de persistencia, y un doble en
// memoria probaria el doble, no el repositorio.
[assembly: InternalsVisibleTo("PirryLedger.Core.Permissions.Tests")]

// Seccion 1.4 del enunciado: la prueba de integracion usa los repositorios
// reales de solicitudes y concesiones sobre un contexto en memoria. Sustituirlos
// por dobles haria que la prueba certificara el doble y no la pieza.
[assembly: InternalsVisibleTo("PirryLedger.Integration.Tests")]
