# Bitácora de categorías del catálogo

## 13 de noviembre de 2026

Franklin pidió un catálogo configurable de dos niveles y aprobó el ADR 021.
Durante el trabajo aclaró que todos los productos necesitan descripción al
crearlos o editarlos. Se incorporaron la migración, las operaciones de
administración y los filtros del catálogo.

El agente ejecutó `dotnet test pirry-ledger.slnx --no-restore`. Pasaron 488
pruebas. En PostgreSQL temporal, la migración conservó el precio, el estado y
la revisión de imagen de un producto existente. El producto quedó sin
subcategoría y la consulta devolvió seis subcategorías de Comidas y cuatro de
Bebidas. La revisión de Franklin queda pendiente.

En la revisión previa al nuevo PR se encapsuló la creación de categorías y
subcategorías y se hizo obligatoria la dependencia de clasificación. La suite
completa pasó con 489 pruebas, incluida la edición de ambos niveles. En
PostgreSQL temporal se verificó que la migración también conserva la clave
y la versión del registro de imagen asociado.
