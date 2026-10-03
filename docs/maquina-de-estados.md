# Máquina de estados de la factura

Esta es la máquina de estados propia del módulo de negocio. En esta iteración
solo se entrega su estructura; las pruebas de la máquina llegan en la semana 8.

La entidad central es `Invoice` y sus estados están declarados en
`InvoiceState`. Todas las transiciones se resuelven en un único componente:
`InvoiceStateMachine`. Una transición que no aparece en la tabla está prohibida
y no cambia el estado de la factura.

| from | to | who executes | condition |
|---|---|---|---|
| `Draft` | `Issued` | Administrador | La factura fue revisada y está lista para entregarse al cliente. |
| `Draft` | `Cancelled` | Administrador | La factura fue creada por error y todavía no fue emitida. |
| `Issued` | `Paid` | Administrador | El cliente realizó el pago. |
| `Issued` | `Cancelled` | Administrador | La factura emitida debe anularse conforme a la operación del negocio. |

## Transiciones prohibidas y terminales

- `Paid -> Cancelled` está prohibida explícitamente: una factura pagada no vuelve
  a un estado anterior.
- `Cancelled -> Draft`, `Cancelled -> Issued` y `Cancelled -> Paid` están
  prohibidas: `Cancelled` es un estado terminal.
- `Paid` también es terminal porque no tiene transiciones salientes declaradas.
