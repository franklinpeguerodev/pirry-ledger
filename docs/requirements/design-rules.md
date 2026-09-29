# Design rules (RD)

Source: Requerimientos del Core, sección 1. They apply to the whole system, including the business module, and are evaluated in code review and in the final demo. A criterion that does not pass means the requirement is not done, even if the code runs.

| ID | Requisito | Criterio de aceptación |
|---|---|---|
| RD-01 | Cada pieza del Core es un componente con una responsabilidad única y una interfaz explícita. | Puedes decir, para cada pieza, qué hace y qué operaciones expone, sin describir el funcionamiento interno de otra pieza. |
| RD-02 | La lógica de negocio no vive en la capa de presentación ni dentro del manejador de la petición. | Cambiar la interfaz de usuario no obliga a reescribir ninguna regla de negocio. |
| RD-03 | El Core no depende del módulo de negocio; la dependencia va en un solo sentido. | Si eliminas el módulo de negocio del proyecto, el Core sigue construyéndose y ejecutándose. |
| RD-04 | Las transiciones de estado se resuelven en un solo punto del código. | Agregar una transición nueva se hace modificando un único componente; no hay validaciones de estado repetidas en varios lugares. |
| RD-05 | Las contraseñas se almacenan con hash. | Leyendo el almacenamiento directamente no es posible recuperar ninguna contraseña. |
| RD-06 | La autorización se verifica del lado del servidor en cada operación. | Ocultar la opción en la interfaz no basta: invocar la operación directamente también se rechaza. |
| RD-07 | Toda entrada externa se valida antes de usarse. | Datos ausentes, de tipo incorrecto o fuera de rango producen un rechazo controlado, no una excepción sin manejar. |
| RD-08 | Los errores devuelven un mensaje comprensible sin exponer detalles internos. | Ningún mensaje visible al usuario contiene trazas de pila, rutas de archivos ni consultas. |
| RD-09 | Los datos persisten fuera del proceso. | Reiniciar la aplicación no pierde información. |
| RD-10 | Las credenciales y claves se leen de variables de entorno. | No hay ninguna credencial escrita en el repositorio, ni en el historial de commits. |
| RD-11 | Las fechas y horas se registran con el mismo criterio en todo el sistema. | Dos registros creados en el mismo instante por piezas distintas muestran la misma hora. |
| RD-12 | Cada pieza del Core puede probarse sin levantar la aplicación completa. | Existe al menos una prueba de esa pieza que corre sin interfaz de usuario. |

## Notes for Practice 1

Practice 1 adds detail to some of these: RD-05 (two users with the same password must not share the stored value), RD-07 (an empty or malformed email produces a controlled rejection), RD-08, RD-09 and RD-10 all apply to Access control.