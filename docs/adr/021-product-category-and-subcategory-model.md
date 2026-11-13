# ADR 021 - Product Category and Subcategory Model

## Estado

Aceptado por Franklin.

## Contexto

El catálogo necesita agrupar productos en dos niveles configurables. El menú
inicial usa Comidas y Bebidas. El Administrador debe poder ampliar la
clasificación desde los datos.

## Decisión

El módulo Business guarda categorías y subcategorías en dos tablas. Cada
subcategoría pertenece a una categoría y cada producto puede referenciar una
subcategoría. Los productos existentes conservan sus datos y aparecen como
Sin categoría hasta que se les asigne una subcategoría.

El servidor exige una descripción al crear o editar cualquier producto.

## Alternativas consideradas

Una tabla con referencia a sí misma permitiría más niveles. Para mantener el
menú de dos niveles habría que validar la profundidad y prevenir ciclos. Dos
tablas expresan los niveles y su relación mediante claves foráneas.

## Consecuencias

Los filtros consultan la clasificación persistida. Agregar una categoría o
subcategoría es una operación de administración. Agregar un tercer nivel
requeriría revisar el modelo.
