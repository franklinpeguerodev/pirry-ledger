# Business module: state machine and operations

Source: Requerimientos del Core, sección 9; Práctica 1, sección 1.6; and Práctica 3, sección 1.1. The remaining RF-NEG requirements are added when an iteration needs them.

| ID | Requisito | Criterio de aceptación |
|---|---|---|
| RF-NEG-02 | Al menos cinco funcionalidades separables. | Cada una se puede probar por separado de las demás y tu README explica cómo probarla. |
| RF-NEG-03 | Máquina de estados propia, entre 3 y 5 estados. | Los estados están declarados en un solo lugar del código. |
| RF-NEG-04 | Al menos una transición prohibida de forma explícita. | Intentarla se rechaza y el estado no cambia. |
| RF-NEG-05 | Al menos un estado terminal del que no se puede salir. | Ninguna transición parte de ese estado. |
| RF-NEG-06 | Sus operaciones están protegidas por Control de acceso. | Ninguna operación del negocio se ejecuta sin sesión válida y sin verificación de rol. |
| RF-NEG-09 | Su máquina de estados es independiente de la de Gestión de permisos. | Cambiar los estados de Gestión de permisos no obliga a cambiar los del negocio. |

Related design rules: RD-04 (transitions are resolved in a single point of the code), RD-07 (invalid input is a controlled rejection) and RD-09 (business data survive a restart); see `design-rules.md`.

## What Practice 1 delivers (structure only, no tests yet)

- The central entity of the domain exists in the data model, with its state attribute.
- The states are declared in one place in the code (RF-NEG-03).
- The allowed transitions are declared in one place (RD-04), with at least one explicitly forbidden transition (RF-NEG-04) and at least one terminal state (RF-NEG-05).
- A file `docs/maquina-de-estados.md` (exact name required by the assignment) with the transitions table: from, to, who executes it, condition. Like the Gestión de permisos table in the Core requirements.

## What Practice 3 delivers

- Seven states and eleven allowed transitions, with the other thirty-eight pairs rejected and the state unchanged. The table and its conditions are documented in `docs/maquina-de-estados.md` and resolved in `InvoiceStateMachine` (RF-NEG-03, RF-NEG-04, RF-NEG-05, RD-04).
- `Draft` is the initial state of the aggregate in memory and is never persisted: the server receives the invoice complete and stores it already issued.
- A suite with one test per row of the allowed table, one per row of the forbidden table and one per terminal state, plus the conditions of each row (RF-NEG-03, RF-NEG-04, RF-NEG-05).
- At least five separable features of the invoicing module, each explained in the README: issue, collect, resolve pending payments, pay a debt, cancel with a permission, mark as not delivered, consult invoices and read the edit history (RF-NEG-02).
- Every operation requires a valid session and a role check (RF-NEG-06).
- The invoice machine references nothing of Gestión de permisos (RF-NEG-09).

## Context

The business module has its own state machine, independent from the Core's permission requests one. The project has two state machines tested separately, so there are two test suites, not one. The cancellation of a `Paid` or `InDebt` invoice needs a permission granted by Gestión de permisos, but the permission is not part of the machine: the use case consults it in a single line and the machine only decides the transition.
