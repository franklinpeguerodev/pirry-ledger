# Máquinas de estados

El sistema tiene dos máquinas de estados independientes: la de la factura, en el
módulo de negocio, y la de la solicitud de permiso, en la pieza de permisos. Cada
una resuelve todas sus transiciones en un único componente, y una transición que
no aparece en su tabla está prohibida: el sistema la rechaza y el estado no
cambia (RD-04).

## Máquina de estados de la factura

Es la máquina de estados propia del módulo de negocio. Su estructura se entregó
con la Práctica 1; las pruebas de la máquina llegan en la semana 8.

La entidad central es `Invoice` y sus estados están declarados en
`InvoiceState`. Todas las transiciones se resuelven en un único componente:
`InvoiceStateMachine`.

| desde | hacia | quién la ejecuta | condición |
|---|---|---|---|
| `Draft` | `Issued` | Administrador | La factura fue revisada y está lista para entregarse al cliente. |
| `Draft` | `Cancelled` | Administrador | La factura fue creada por error y todavía no fue emitida. |
| `Issued` | `Paid` | Administrador | El cliente realizó el pago. |
| `Issued` | `Cancelled` | Administrador | La factura emitida debe anularse conforme a la operación del negocio. |

### Transiciones prohibidas y terminales

- `Paid -> Cancelled` está prohibida explícitamente: una factura pagada no vuelve
  a un estado anterior.
- `Cancelled -> Draft`, `Cancelled -> Issued` y `Cancelled -> Paid` están
  prohibidas: `Cancelled` es un estado terminal.
- `Paid` también es terminal porque no tiene transiciones salientes declaradas.

## Máquina de estados de la solicitud de permiso

Es la máquina de la pieza de permisos. La entidad central es
`PermissionRequest` y sus estados están declarados en `PermissionRequestState`.
Todas las transiciones se resuelven en un único componente:
`PermissionRequestStateMachine`, cuya tabla `AllowedTransitions` es la única
fuente de verdad. Ningún caso de uso ni endpoint valida el estado por su cuenta.

| desde | hacia | quién la ejecuta | condición |
|---|---|---|---|
| `Pending` | `Approved` | Administrador | La solicitud está en `Pending`. Queda registrado quién aprobó y cuándo. |
| `Approved` | `Applied` | El sistema | Automática, en la misma operación que aprueba. Queda registrado el instante de aplicación y se crea la concesión del permiso. |
| `Pending` | `Rejected` | Administrador | Exige un motivo escrito y no vacío. |

### Transiciones prohibidas y terminales

- De los dieciséis pares posibles entre los cuatro estados, la tabla permite
  exactamente tres. Las otras trece combinaciones están prohibidas, incluidas
  las que dejan el estado igual (`Pending -> Pending`, por ejemplo).
- `Pending -> Applied` está prohibida: la aplicación ocurre después de la
  aprobación, no en lugar de ella.
- `Approved -> Pending` y `Approved -> Rejected` están prohibidas: una solicitud
  ya resuelta no vuelve atrás ni se resuelve dos veces. Es lo que hace que
  aprobar dos veces la misma solicitud se rechace sin cambiar el estado
  (RF-GP-06).
- `Rejected -> Pending`, `Rejected -> Approved` y `Rejected -> Applied` están
  prohibidas.
- `Applied` y `Rejected` son terminales: no tienen transiciones salientes
  declaradas.

RF-GP-07 no es una transición. La regla de que nadie resuelve su propia
solicitud la comprueba el caso de uso antes de pedir el cambio de estado, y
compara identidades (el `Id` del solicitante contra el `Id` del resolutor), no
roles.

### Independencia de la otra máquina

Las dos máquinas no comparten código, ni tipos, ni enums (RF-NEG-09). Viven en
proyectos distintos y se compilan por separado: `InvoiceState` y
`InvoiceStateMachine` en el módulo de negocio, `PermissionRequestState` y
`PermissionRequestStateMachine` en la pieza de permisos, cada una con su propia
excepción de transición inválida. Si una gana un estado, la otra no se entera, y
al revés.
