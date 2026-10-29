# Máquinas de estados

El sistema tiene dos máquinas de estados independientes: la de la factura, en el
módulo de negocio, y la de la solicitud de permiso, en la pieza de permisos. Cada
una resuelve todas sus transiciones en un único componente, y una transición que
no aparece en su tabla está prohibida: el sistema la rechaza y el estado no
cambia (RD-04).

## Máquina de estados de la factura

Es la máquina de estados propia del módulo de negocio. La entidad central es
`Invoice` y sus estados están declarados en `InvoiceState`. Todas las
transiciones se resuelven en un único componente: `InvoiceStateMachine`, cuya
tabla `AllowedTransitions` es la única fuente de verdad.

Siete estados (RF-NEG-03): `Draft`, `Issued`, `PendingVerification`, `Paid`,
`InDebt`, `Cancelled` y `NotDelivered`. `Issued` se le muestra al usuario como
"pendiente".

`Draft` es el estado inicial del agregado en memoria, pero **no se persiste**:
el borrador vive solo en el cliente, porque guardarlo costaría una escritura por
cada ítem tocado y dejaría filas que nadie cierra. El servidor recibe la factura
completa y la guarda ya emitida, así que la fila nace en `Issued` y la
transición `Draft -> Issued` ocurre en la misma operación que la emisión.

### Transiciones permitidas

De los 49 pares posibles entre los siete estados, la tabla permite exactamente
once:

| desde | hacia | quién la ejecuta | condición |
|---|---|---|---|
| `Draft` | `Issued` | Quien cobra | Tiene al menos un ítem. Aquí se marca si es delivery. |
| `Issued` | `Paid` | Quien cobra | Los pagos cubren el total y ninguno es transferencia. |
| `Issued` | `PendingVerification` | Quien cobra | Algún pago es transferencia. |
| `Issued` | `InDebt` | Quien cobra | Exige el nombre de la persona. |
| `Issued` | `Cancelled` | Quien cobra | Sin permiso: la factura todavía no se cobró. |
| `Issued` | `NotDelivered` | Quien cobra | La factura es de delivery. |
| `PendingVerification` | `Paid` | Administrador | La transferencia llegó. |
| `PendingVerification` | `InDebt` | Administrador | Transferencia rechazada. Exige el nombre de la persona. |
| `InDebt` | `Paid` | Administrador | Los abonos cubren el total de la deuda. |
| `Paid` | `Cancelled` | Quien solicitó el permiso | Existe un permiso aprobado para esa factura; se consume al anular. |
| `InDebt` | `Cancelled` | Quien solicitó el permiso | Igual que la anterior. |

Las condiciones de las filas viven en la propia máquina, una comprobación por
fila y aislada en su método: si una se quita, la prueba de esa fila se pone roja.
La máquina recibe los hechos de la tentativa en `InvoiceTransitionContext`
(cantidad de ítems, si es delivery, nombre del deudor, total, pagos acumulados y
si hay transferencia); no lee colecciones ni base de datos, así que se prueba sin
levantar la aplicación (RD-12).

La consulta del permiso de anulación **no** es una condición de la tabla: depende
de la factura concreta y la resuelve el caso de uso con una sola línea antes de
pedir la transición. La máquina no conoce la pieza de permisos (RF-NEG-09).

### Transiciones prohibidas

Los otros 38 pares se rechazan con `InvalidInvoiceTransitionException` y el
estado no cambia (RF-NEG-04). Agrupados por su origen:

- Desde `Draft` (6): hacia sí mismo, `PendingVerification`, `Paid`, `InDebt`,
  `Cancelled` y `NotDelivered`. Un borrador solo se emite.
- Desde `Issued` (2): hacia `Draft` y hacia sí mismo. Una factura emitida no
  vuelve a borrador.
- Desde `PendingVerification` (5): hacia `Draft`, `Issued`, sí mismo, `Cancelled`
  y `NotDelivered`. Una transferencia pendiente se resuelve o no se toca.
- Desde `Paid` (6): hacia `Draft`, `Issued`, `PendingVerification`, sí mismo,
  `InDebt` y `NotDelivered`. Una factura pagada solo puede anularse con permiso.
- Desde `InDebt` (5): hacia `Draft`, `Issued`, `PendingVerification`, sí mismo y
  `NotDelivered`.
- Desde `Cancelled` (7) y desde `NotDelivered` (7): todos. Son terminales.

Están incluidas las siete transiciones que dejan el estado igual
(`Draft -> Draft`, `Paid -> Paid`, …): la máquina no es idempotente y repetir una
operación falla en lugar de "no hacer nada".

`Paid -> Cancelled` era la transición prohibida de la Práctica 1. Ahora está
permitida con un permiso aprobado para esa factura, y por eso `Paid` dejó de ser
terminal.

### Estados terminales

`Cancelled` y `NotDelivered` son terminales (RF-NEG-05): no tienen ninguna
transición saliente declarada. Si el cliente de una factura marcada como no
entregada aparece después, se emite una factura nueva; la anterior no revive.

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
al revés. Dos pruebas lo comprueban, una en cada suite: el módulo de negocio no
referencia ningún ensamblado de permisos ni de control de acceso, y la máquina de
permisos no vive en un ensamblado de negocio.
